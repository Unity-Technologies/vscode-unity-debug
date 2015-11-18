using System;
using Newtonsoft.Json;
using Mono.Debugging.Soft;
using Mono.Debugging.Client;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Dynamic;
using System.Collections.Generic;

namespace UnityDebug
{
	public class UnityDebugSession
	{
		bool initialized = false;
		SoftDebuggerSession session;
		ProcessInfo activeProcess;
		VariableMap<ObjectValue[]> variableReferences = new VariableMap<ObjectValue[]>();
		private Dictionary<int, Thread> activeThreads = new Dictionary<int, Thread> ();

		static bool pathFormatPath = false;
		static bool linesStartAt1 = false;

		public delegate void SendEventHandler(Event @event);
		private event SendEventHandler sendEventEvent;

		public event SendEventHandler OnSendEvent
		{
			add
			{
				sendEventEvent += value;
			}
			remove
			{
				sendEventEvent -= value;
			}
		}

		public Response HandleRequest(Request request)
		{
			if (request.command == "initialize") {
				initialized = Initialize ((string)request.arguments.adapterID, (string)request.arguments.pathFormat, (bool)request.arguments.linesStartAt1);

				if (!initialized) 
					return Response.Failure (request, "Debugger type is " + (string)request.arguments.adapterId + " and not 'unity'");

				// Send event that indicates that the debugee is ready to accept SetBreakpoint calls.
				SendEvent ("initialized");
				return Response.Default (request);
			}

			if (!initialized)
				return Response.Failure (request, "Unity debugger not initialized");

			return HandleRequestInitialized (request);
		}

		Response HandleRequestInitialized(Request request)
		{
			switch (request.command) 
			{
				case "launch":
				{
					string target = request.arguments.name;
					var errorMessage = Connect (target);
					return errorMessage != null ? Response.Failure (request, errorMessage) : Response.Default (request);
				}

				case "disconnect":
				{
					Disconnect ();
					return Response.Default (request);
				}

				case "setBreakpoints":
				{
					JArray lines = request.arguments.lines;
					SetBreakpoint ((string)request.arguments.source.path, lines.Select (elem => (int)elem).ToArray ());
					return Response.Default (request);
				}

				case "setExceptionBreakpoints":
				{
					return Response.Default (request);
				}

				case "continue":
				{
					Continue ();
					return Response.Default (request);
				}

				case "pause":
				{
					Pause ();
					return Response.Default (request);
				}

				case "next":
				{
					StepOver ();
					return Response.Default (request);
				}

				case "stepIn":
				{
					StepIn ();
					return Response.Default (request);
				}

				case "stepOut":
				{
					StepOut ();
					return Response.Default (request);
				}

				case "threads":
				{
					var threads = Threads ();
		
					if (threads != null) {
						dynamic body = new ExpandoObject ();
						body.threads = threads;
						return Response.Success (request, body);
					} else
						return Response.Failure (request, "Could not get threads");
				}

				case "stackTrace":
				{
					int threadId = (int)request.arguments.threadId;
					int levels = (int)request.arguments.levels;
					var frames = StackTrace (threadId, levels);

					if (frames != null) {
						dynamic body = new ExpandoObject ();
						body.stackFrames = frames;
						return Response.Success (request, body);
					} else
						return Response.Failure (request, "Could not get stack trace for threadId " + threadId);
				}

				case "scopes":
				{
					int frameId = (int)request.arguments.frameId;
					var scopes = Scopes (frameId);

					if (scopes != null) {
						dynamic body = new ExpandoObject ();
						body.scopes = scopes;
						return Response.Success (request, body);
					} else
						return Response.Failure (request, "Could not get scope for frameId " + frameId);
				}

				case "variables":
				{
					int variableReference = (int)request.arguments.variablesReference;
					var variables = ChildVariables (variableReference);

					if (variables != null) {
						dynamic body = new ExpandoObject ();
						body.variables = variables;
						return Response.Success (request, body);
					} else
						return Response.Failure (request, "Could not get variables for variable reference  " + variableReference);
				}
				default:
					Log.Write (">>> ERROR: Unhandled request: " + request.command);
					return Response.Failure (request, "Unhandled request: '" + request.command + "'");
			}
		}

		bool Initialize(string adapterID, string pathFormat, bool startAt1)
		{
			if (adapterID != "unity")
				return false;

			pathFormatPath = (pathFormat == "path");
			linesStartAt1 = startAt1;

			session = new SoftDebuggerSession ();
			session.ExceptionHandler += ExceptionHandler;

			session.TargetEvent += (sender, e) => Log.Write ("Debugger Event: " + e.Type);
			session.LogWriter = (isStdErr, text) => Log.Write ("Debugger Log: " + text);
			session.OutputWriter = (isStdErr, text) => Log.Write ("Debugger Output: " + text);

			session.TargetReady += (object sender, TargetEventArgs e) => activeProcess = session.GetProcesses ().SingleOrDefault<ProcessInfo> ();

			session.TargetThreadStarted += delegate(object sender, TargetEventArgs e) 
			{
				var thread = e.Thread;

				if(!activeThreads.ContainsKey((int)thread.Id))
				{
					activeThreads.Add((int)thread.Id, new Thread((int)thread.Id, thread.Name));
					SendThreadEvent("started", (int)thread.Id);
				}
			};

			session.TargetThreadStopped += delegate(object sender, TargetEventArgs e) 
			{
				var thread = e.Thread;

				if(activeThreads.ContainsKey((int)thread.Id))
				{
					activeThreads.Remove((int)thread.Id);
					SendThreadEvent("exited", (int)thread.Id);
				}
			};

			session.TargetHitBreakpoint += delegate (object sender, TargetEventArgs e) 
			{
				Log.DebugWrite("Debugger TargetHitBreakpoint");
				OnStopped();
				SendStoppedEvent("breakpoint", (int)e.Thread.Id);
			};

			session.TargetStopped += delegate (object sender, TargetEventArgs e) 
			{
				Log.DebugWrite("Debugger TargetStopped");
				OnStopped();
				SendStoppedEvent("step", (int)e.Thread.Id);
			};

			return true;
		}

		string Connect(string target)
		{
			Log.DebugWrite ("Connect arguments: " + target);

			var ip = IPAddress.Loopback;
			var port = 0;

			var targetLowercase = target.ToLower ();

			if (targetLowercase.Contains ("unity") && targetLowercase.Contains ("editor")) 
			{
				port = FindUnityEditorPort ();

				if (port == -1)
					return "No Unity Editor process found";
			} 
			else 
			{
				return "Cannot connect to '" + target + "'. Unknown target.";
			}

			SoftDebuggerConnectArgs startArgs = new SoftDebuggerConnectArgs (string.Empty, ip, port) {
				MaxConnectionAttempts = 3,
				TimeBetweenConnectionAttempts = 100
			};

			session.Run (new SoftDebuggerStartInfo (startArgs), 
				new DebuggerSessionOptions { EvaluationOptions = EvaluationOptions.DefaultOptions });

			return null;
		}

		void Disconnect()
		{
			// FIXME: Send VM_DISPOSE to debugger agent
			session = null;
		}

		void SetBreakpoint(string path, int[] lines)
		{
			var fileBreakpoints = session.Breakpoints.GetBreakpointsAtFile (path);

			foreach (var bp in fileBreakpoints)
				if (lines.All (l => l != bp.Line)) 
				{
					Log.DebugWrite ("Remove Breakpoint " + bp.FileName + ":" + bp.Line);
					session.Breakpoints.Remove (bp);
				}

			foreach (int line in lines) 
				if (!session.Breakpoints.OfType<Breakpoint> ().Any (b => b.FileName == path && b.Line == line )) 
				{
					Log.DebugWrite ("Add Breakpoint " + path + ":" + line);
					session.Breakpoints.Add (path, line);
				}
		}

		void Continue ()
		{
			if (!session.IsRunning && !session.HasExited)
				session.Continue ();
		}

		void Pause ()
		{
			if (session.IsRunning)
				session.Stop ();
		}

		void StepOver()
		{
			if (!session.IsRunning && !session.HasExited)
				session.NextLine ();
		}

		void StepIn()
		{
			if (!session.IsRunning && !session.HasExited)
				session.StepLine ();
		}

		void StepOut()
		{
			if (!session.IsRunning && !session.HasExited)
				session.Finish ();
		}

		Thread[] Threads ()
		{
			if (activeProcess == null)
				return null;

			// Use dictionary to get thread array sorted by thread id.
			SortedDictionary<int, Thread> dictionary = new SortedDictionary<int, Thread> ();

			foreach(var threadInfo in activeProcess.GetThreads())
				dictionary.Add ((int)threadInfo.Id, new Thread ((int)threadInfo.Id, threadInfo.Name));
		
			return dictionary.Values.ToArray ();
		}

		ThreadInfo GetThread (int threadId)
		{
			if (activeProcess != null) {
				ThreadInfo[] threads = activeProcess.GetThreads ();
				for (int i = 0; i < threads.Length; i++) {
					ThreadInfo threadInfo = threads [i];
					if (threadInfo.Id == (long)threadId) {
						return threadInfo;
					}
				}
			}
			return null;
		}

		StackFrame[] StackTrace (int threadId, int maxLevels)
		{
			var threadInfo = session.ActiveThread;

			if (threadInfo.Id != threadId) 
			{
				threadInfo = GetThread (threadId);

				if (threadInfo == null)
					return null;

				threadInfo.SetActive ();
			}

			List<StackFrame> frames = new List<StackFrame> ();

			var backtrace = threadInfo.Backtrace;

			if (backtrace != null) {
				for (int i = 0; i < Math.Min (backtrace.FrameCount, maxLevels); ++i) {
					var stackFrame = backtrace.GetFrame (i);
					var methodName = stackFrame.SourceLocation.MethodName;
					int line = stackFrame.SourceLocation.Line;
					int column = stackFrame.SourceLocation.Column;
					var path = stackFrame.SourceLocation.FileName;

					int frameIndex = (backtrace.FrameCount - i);

					frames.Add (new StackFrame (threadId * 1000 + frameIndex, methodName, new Source (path), line, column));
				}
			}

			return frames.ToArray ();
		}

		Scope[] Scopes(int frameId)
		{
			int threadId = frameId / 1000;
			int frameIndex = frameId % 1000;

			var threadInfo = session.ActiveThread;

			if (threadInfo.Id != threadId) 
			{
				threadInfo = GetThread (threadId);

				if (threadInfo == null)
					return null;

				threadInfo.SetActive ();
			}

			var backtrace = threadInfo.Backtrace;
			frameIndex -= backtrace.FrameCount;

			if (frameIndex > backtrace.FrameCount)
				return null;

			List<Scope> scopes = new List<Scope> ();

			var stackFrame = backtrace.GetFrame (frameIndex);

			var arguments = stackFrame.GetParameters ().Where (p => p != null).ToArray();

			if (arguments.Length > 0)
				scopes.Add (new Scope ("Arguments", variableReferences.Add(arguments)));

			var locals = stackFrame.GetAllLocals ();

			if (locals.Length > 0)
				scopes.Add (new Scope ("Locals", variableReferences.Add(locals)));

			return scopes.ToArray ();
		}

		Variable[] ChildVariables(int variableReference)
		{
			ObjectValue[] objectValues;

			if (!variableReferences.TryGet (variableReference, out objectValues))
				return null;

			var variables = new List<Variable> ();

			foreach(var ov in objectValues)
			{
				// IsError is set for deprecated members.
				if (ov.Name == "?" || ov.IsError)
					continue;
				
				ov.WaitHandle.WaitOne ();

				int reference = ov.HasChildren ? variableReferences.Add (ov.GetAllChildren ()) : 0;
				variables.Add (new Variable (string.Format ("{0} {1}", ov.TypeName, ov.Name), ov.DisplayValue, reference));
			}

			return variables.ToArray ();
		}

		void SendEvent(string type,  dynamic body = null)
		{
			Log.DebugWrite ("Sending event: '" + type + "'");

			if(sendEventEvent != null)
				sendEventEvent(new Event(type, body));
		}

		void SendThreadEvent(string reason, int threadId)
		{
			dynamic body = new ExpandoObject ();

			body.reason = reason;
			body.threadId = threadId;

			SendEvent ("thread", body);
		}

		void SendStoppedEvent(string reason, int threadId, string text = null)
		{
			dynamic body = new ExpandoObject ();

			body.reason = reason;
			body.threadId = threadId;

			if (text != null)
				body.text = text;

			SendEvent ("stopped", body);
		}

		void OnStopped ()
		{
			variableReferences.Clear ();
		}

		int FindUnityEditorPort()
		{
			var processes = Process.GetProcesses ();

			if (null != processes) {
				foreach (Process p in processes) {
					try {
						if ((p.ProcessName.StartsWith ("unity", StringComparison.OrdinalIgnoreCase) ||
							p.ProcessName.Contains ("Unity.app")) &&
							!p.ProcessName.Contains ("UnityShader") &&
							!p.ProcessName.Contains ("UnityHelper") &&
							!p.ProcessName.Contains ("Unity Helper")) 
						{
							return 56000 + (p.Id % 1000);
						}
					} catch {
						// Don't care; continue
					}
				}
			}

			return -1;
		}

		bool ExceptionHandler(Exception ex)
		{
			if (ex is DebuggerException) 
			{
				Log.Write ("Debugger Exception: " + ex);
				return true;
			}

			return false;
		}
	}
}

