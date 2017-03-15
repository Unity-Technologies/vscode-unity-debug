/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Copyright (c) Unity Technologies.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using Mono.Debugger.Client;
using Mono.Debugging.Client;
using VSCodeDebug;
using MonoDevelop.Debugger.Soft.Unity;

namespace UnityDebug
{
	internal class UnityDebugSession : DebugSession
	{

		private readonly string[] MONO_EXTENSIONS = new String[] { ".cs" };
		private const int MAX_CHILDREN = 100;

		private Handles<ObjectValue[]> _variableHandles;
		private Handles<Mono.Debugging.Client.StackFrame> _frameHandles;
		private ObjectValue _exception;
		private Dictionary<int, VSCodeDebug.Thread> _seenThreads = new Dictionary<int, VSCodeDebug.Thread>();
		private bool _terminated = false;

		IUnityDbgConnector unityDebugConnector;
			
		public UnityDebugSession() : base(true)
		{
			_variableHandles = new Handles<ObjectValue[]>();
			_frameHandles = new Handles<Mono.Debugging.Client.StackFrame>();
			_seenThreads = new Dictionary<int, VSCodeDebug.Thread>();

			Configuration.Current.MaxConnectionAttempts = 10;
			Configuration.Current.ConnectionAttemptInterval = 500;

			// install an event handler in SDB
			Debugger.Callback = (type, threadinfo, text) => {
				int tid;
				switch (type) {
				case "TargetStopped":
					Stopped();
					SendEvent(CreateStoppedEvent("step", threadinfo));
					break;

				case "TargetHitBreakpoint":
					Stopped();
					SendEvent(CreateStoppedEvent("breakpoint", threadinfo));
					break;

				case "TargetExceptionThrown":
				case "TargetUnhandledException":
					Stopped();
					ExceptionInfo ex = Debugger.ActiveException;
					if (ex != null) {
						_exception = ex.Instance;
					}
					SendEvent(CreateStoppedEvent("exception", threadinfo, Debugger.ActiveException.Message));
					break;

				case "TargetExited":
					Terminate("target exited");
					break;

				case "TargetThreadStarted":
					tid = (int)threadinfo.Id;
					lock (_seenThreads) {
						_seenThreads[tid] = new VSCodeDebug.Thread(tid, threadinfo.Name);
					}
					SendEvent(new ThreadEvent("started", tid));
					break;

				case "TargetThreadStopped":
					tid = (int)threadinfo.Id;
					lock (_seenThreads) {
						_seenThreads.Remove(tid);
					}
					SendEvent(new ThreadEvent("exited", tid));
					break;

				case "Output":
					SendOutput("stdout", text);
					break;

				case "ErrorOutput":
					SendOutput("stderr", text);
					break;

				default:
					SendEvent(new Event(type));
					break;
				}
			};
		}

		public override void Initialize(Response response, dynamic args)
		{
			SendOutput("stdout", "UnityDebug: Initializing");

			SendResponse(response, new Capabilities() {
				// This debug adapter does not need the configurationDoneRequest.
				supportsConfigurationDoneRequest = false,

				// This debug adapter does not support function breakpoints.
				supportsFunctionBreakpoints = false,

				// This debug adapter doesn't support conditional breakpoints.
				supportsConditionalBreakpoints = false,

				// This debug adapter does support a side effect free evaluate request for data hovers.
				supportsEvaluateForHovers = true,

				// This debug adapter does not support exception breakpoint filters
				exceptionBreakpointFilters = new dynamic[0]
			});

			// Mono Debug is ready to accept breakpoints immediately
			SendEvent(new InitializedEvent());
		}

		public override void Launch(Response response, dynamic args)
		{
			Attach (response, args);
		}

		public override void Attach(Response response, dynamic args)
		{
			string name = getString (args, "name");

			SendOutput("stdout", "UnityDebug: Searching for Unity process '" + name + "'");

			var processes = UnityAttach.GetAttachableProcesses (name).ToArray ();

			if (processes == null) {
				SendErrorResponse(response, 8001, "Unknown target name '{_name}'. Did you mean 'Unity Editor'?", new { _name = name});
				return;
			}

			if (processes.Length == 0) {
				SendErrorResponse (response, 8001, "Could not find target name '{_name}'. Is it running?", new { _name = name});
				return;
			}

			if (processes.Length > 1) {
				SendErrorResponse (response, 8002, "Multiple targets with name '{_name}' running. Unable to connect.", new { _name = name});

				SendOutput("stdout", "UnityDebug: Multiple targets with name '" + name + "' running. Unable to connect");

				foreach(var p in processes)
					SendOutput("stdout", "UnityDebug: Found Unity process '" + p.Name + "' (" + p.Id + ")\n");

				return;
			}

			var process = processes [0];

			var attachInfo = UnityProcessDiscovery.GetUnityAttachInfo (process.Id, ref unityDebugConnector);

			Debugger.Connect (attachInfo.Address, attachInfo.Port);

			SendOutput("stdout", "UnityDebug: Attached to Unity process '" + process.Name + "' (" + process.Id + ")\n");
			SendResponse(response);
		}

		public override void Disconnect(Response response, dynamic args)
		{
			if (unityDebugConnector != null) {
				unityDebugConnector.OnDisconnect ();
				unityDebugConnector = null;
			}

			Debugger.Disconnect();
			SendOutput("stdout", "UnityDebug: Disconnected");
			SendResponse(response);
		}

		public override void Continue(Response response, dynamic args)
		{
			CommandLine.WaitForSuspend();
			Debugger.Continue();
			SendResponse(response);
		}

		public override void Next(Response response, dynamic args)
		{
			CommandLine.WaitForSuspend();
			Debugger.StepOverLine();
			SendResponse(response);
		}

		public override void StepIn(Response response, dynamic args)
		{
			CommandLine.WaitForSuspend();
			Debugger.StepIntoLine();
			SendResponse(response);
		}

		public override void StepOut(Response response, dynamic args)
		{
			CommandLine.WaitForSuspend();
			Debugger.StepOutOfMethod();
			SendResponse(response);
		}

		public override void Pause(Response response, dynamic args)
		{
			Debugger.Pause();
			SendResponse(response);
		}

		public override void SetBreakpoints(Response response, dynamic args)
		{
			string path = null;
			if (args.source != null) {
				string p = (string)args.source.path;
				if (p != null && p.Trim().Length > 0) {
					path = p;
				}
			}
			if (path == null) {
				SendErrorResponse(response, 3010, "setBreakpoints: property 'source' is empty or misformed", null, false, true);
				return;
			}
			path = ConvertClientPathToDebugger(path);

			if (!HasMonoExtension(path)) {
				// we only support breakpoints in files mono can handle
				SendResponse(response, new SetBreakpointsResponseBody());
				return;
			}

			var clientLines = args.lines.ToObject<int[]>();
			HashSet<int> lin = new HashSet<int>();
			for (int i = 0; i < clientLines.Length; i++) {
				lin.Add(ConvertClientLineToDebugger(clientLines[i]));
			}

			// find all breakpoints for the given path and remember their id and line number
			var bpts = new List<Tuple<int, int>>();
			foreach (var be in Debugger.Breakpoints) {
				var bp = be.Value as Mono.Debugging.Client.Breakpoint;
				if (bp != null && string.Equals(bp.FileName, path, StringComparison.OrdinalIgnoreCase)) {
					bpts.Add(new Tuple<int,int>((int)be.Key, (int)bp.Line));
				}
			}

			HashSet<int> lin2 = new HashSet<int>();
			foreach (var bpt in bpts) {
				if (lin.Contains(bpt.Item2)) {
					lin2.Add(bpt.Item2);
				}
				else {
					// Console.WriteLine("cleared bpt #{0} for line {1}", bpt.Item1, bpt.Item2);

					BreakEvent b;
					if (Debugger.Breakpoints.TryGetValue(bpt.Item1, out b)) {
						Debugger.Breakpoints.Remove(bpt.Item1);
						Debugger.BreakEvents.Remove(b);
					}
				}
			}

			for (int i = 0; i < clientLines.Length; i++) {
				var l = ConvertClientLineToDebugger(clientLines[i]);
				if (!lin2.Contains(l)) {
					var id = Debugger.GetBreakpointId();
					Debugger.Breakpoints.Add(id, Debugger.BreakEvents.Add(path, l));
					// Console.WriteLine("added bpt #{0} for line {1}", id, l);
				}
			}

			var breakpoints = new List<VSCodeDebug.Breakpoint>();
			foreach (var l in clientLines) {
				breakpoints.Add(new VSCodeDebug.Breakpoint(true, l));
			}

			response.SetBody(new SetBreakpointsResponseBody(breakpoints));
		}

		public override void StackTrace(Response response, dynamic args)
		{
			int maxLevels = getInt(args, "levels", 10);
			int threadReference = getInt(args, "threadId", 0);

			CommandLine.WaitForSuspend();
			var stackFrames = new List<VSCodeDebug.StackFrame>();

			ThreadInfo thread = Debugger.ActiveThread;
			if (thread.Id != threadReference) {
				// Console.Error.WriteLine("stackTrace: unexpected: active thread should be the one requested");
				thread = FindThread(threadReference);
				if (thread != null) {
					thread.SetActive();
				}
			}

			var bt = thread.Backtrace;
			if (bt != null && bt.FrameCount >= 0) {
				for (var i = 0; i < Math.Min(bt.FrameCount, maxLevels); i++) {

					var frame = bt.GetFrame(i);
					var frameHandle = _frameHandles.Create(frame);

					string name = frame.SourceLocation.MethodName;
					string path = frame.SourceLocation.FileName;
					int line = frame.SourceLocation.Line;
					string sourceName = Path.GetFileName(path);
					if (sourceName == null)
					{
						sourceName = string.Empty;
					}

					var source = new Source(sourceName, ConvertDebuggerPathToClient(path));
					stackFrames.Add(new VSCodeDebug.StackFrame(frameHandle, name, source, ConvertDebuggerLineToClient(line), 0));
				}
			}

			SendResponse(response, new StackTraceResponseBody(stackFrames));
		}

		public override void Scopes(Response response, dynamic args) {

			int frameId = getInt(args, "frameId", 0);
			var frame = _frameHandles.Get(frameId, null);

			var scopes = new List<Scope>();

			if (frame.Index == 0 && _exception != null) {
				scopes.Add(new Scope("Exception", _variableHandles.Create(new ObjectValue[] { _exception })));
			}

			var parameters = new[] { frame.GetThisReference() }.Concat(frame.GetParameters()).Where(x => x != null);
			if (parameters.Any()) {
				scopes.Add(new Scope("Argument", _variableHandles.Create(parameters.ToArray())));
			}

			var locals = frame.GetLocalVariables();
			if (locals.Length > 0) {
				scopes.Add(new Scope("Local", _variableHandles.Create(locals)));
			}

			SendResponse(response, new ScopesResponseBody(scopes));
		}

		public override void Variables(Response response, dynamic args)
		{
			int reference = getInt(args, "variablesReference", -1);
			if (reference == -1) {
				SendErrorResponse(response, 3009, "variables: property 'variablesReference' is missing", null, false, true);
				return;
			}

			CommandLine.WaitForSuspend();
			var variables = new List<Variable>();

			ObjectValue[] children;
			if (_variableHandles.TryGet(reference, out children)) {
				if (children != null && children.Length > 0) {

					bool more = false;
					if (children.Length > MAX_CHILDREN) {
						children = children.Take(MAX_CHILDREN).ToArray();
						more = true;
					}

					if (children.Length < 20) {
						// Wait for all values at once.
						WaitHandle.WaitAll(children.Select(x => x.WaitHandle).ToArray());
						foreach (var v in children) {
							if (v.IsError)
								continue;
							variables.Add(CreateVariable(v));
						}
					}
					else {
						foreach (var v in children) {
							if (v.IsError)
								continue;
							v.WaitHandle.WaitOne();
							variables.Add(CreateVariable(v));
						}
					}

					if (more) {
						variables.Add(new Variable("...", null));
					}
				}
			}

			SendResponse(response, new VariablesResponseBody(variables));
		}

		public override void Threads(Response response, dynamic args)
		{
			var threads = new List<VSCodeDebug.Thread>();
			var process = Debugger.ActiveProcess;
			if (process != null) {
				Dictionary<int, VSCodeDebug.Thread> d;
				lock (_seenThreads) {
					d = new Dictionary<int, VSCodeDebug.Thread>(_seenThreads);
				}
				foreach (var t in process.GetThreads()) {
					int tid = (int)t.Id;
					d[tid] = new VSCodeDebug.Thread(tid, t.Name);
				}
				threads = d.Values.ToList();
			}
			SendResponse(response, new ThreadsResponseBody(threads));
		}

		private static string ParseEvaluate(string expression)
		{
			// Parse expressions created by using "Add Watch" in VS Code.
			// Add Watch expressions examples:
			// Done_PlayerController this.UnityEngine.GameObject gameObject.UnityEngine.SceneManagement.Scene scene.bool isLoaded
			// Done_PlayerController this.UnityEngine.GameObject gameObject. Static members. Non-public members.int OffsetOfInstanceIDInCPlusPlusObject

			// Replace "Static members" and "Non-public members" with strings without spaces, so we can Split the string correctly.
			var exp = expression.Replace ("Static members.", "static-members").Replace ("Non-public members.", "non-public-members");
			var expStrings = exp.Split (' ');
			var parsedExpression = "";

			if (expStrings.Length > 1) 
			{
				foreach (var subexp in expStrings) 
				{
					// Skip static and non public members substrings
					if (subexp.StartsWith ("static-members") || subexp.StartsWith ("non-public-members"))
						continue;

					// If array operator, remove previous '.'
					if (subexp.StartsWith ("["))
						parsedExpression = parsedExpression.Substring (0, parsedExpression.Length - 1);

					int index = subexp.IndexOf ('.');

					if (index > 0) 
						parsedExpression += subexp.Substring (0, index + 1);
				}

				parsedExpression += expStrings.Last ();
				Log.Write ("Parsed Expression: '" + expression + "' -> '" + parsedExpression + "'");
			}
			return parsedExpression;
		}

		public override void Evaluate(Response response, dynamic args)
		{
			string error = null;

			var expression = getString(args, "expression");
			if (expression == null) {
				error = "expression missing";
			} else {
				int frameId = getInt(args, "frameId", -1);
				var frame = _frameHandles.Get(frameId, null);
				if (frame != null) {

					var parsedExpression = ParseEvaluate (expression);

					if (!frame.ValidateExpression(expression) && parsedExpression.Length > 0 && frame.ValidateExpression (parsedExpression))
						expression = parsedExpression;

					if (frame.ValidateExpression(expression)) {
						var val = frame.GetExpressionValue(expression, Debugger.Options.EvaluationOptions);
						val.WaitHandle.WaitOne();

						var flags = val.Flags;
						if (flags.HasFlag(ObjectValueFlags.Error) || flags.HasFlag(ObjectValueFlags.NotSupported)) {
							error = val.DisplayValue;
							if (error.IndexOf("reference not available in the current evaluation context") > 0) {
								error = "not available";
							}
						}
						else if (flags.HasFlag(ObjectValueFlags.Unknown)) {
							error = "invalid expression";
							// maybe user hovered this's member
							if (!expression.StartsWith("this", System.StringComparison.Ordinal)) {
								args["expression"] = "this." + expression;
								Evaluate(response, args);
								return;
							}
						}
						else if (flags.HasFlag(ObjectValueFlags.Object) && flags.HasFlag(ObjectValueFlags.Namespace)) {
							error = "not available";
						}
						else {
							int handle = 0;
							if (val.HasChildren) {
								handle = _variableHandles.Create(val.GetAllChildren());
							}
							SendResponse(response, new EvaluateResponseBody(val.DisplayValue, handle));
							return;
						}
					}
					else {
						error = "invalid expression";
					}
				}
				else {
					error = "no active stackframe";
				}
			}
			SendErrorResponse(response, 3014, "Evaluate request failed ({_reason}).", new { _reason = error } );
		}

		//---- private ------------------------------------------

		private void SendOutput(string category, string data) {
			if (!String.IsNullOrEmpty(data)) {
				if (data[data.Length-1] != '\n') {
					data += '\n';
				}
				SendEvent(new OutputEvent(category, data));
			}
		}

		private void Terminate(string reason) {
			if (!_terminated) {
				SendEvent(new TerminatedEvent());
				_terminated = true;
			}
		}

		private StoppedEvent CreateStoppedEvent(string reason, ThreadInfo ti, string text = null)
		{
			return new StoppedEvent((int)ti.Id, reason, text);
		}

		private ThreadInfo FindThread(int threadReference)
		{
			var process = Debugger.ActiveProcess;
			if (process != null) {
				foreach (var t in process.GetThreads()) {
					if (t.Id == threadReference) {
						return t;
					}
				}
			}
			return null;
		}

		private void Stopped()
		{
			_exception = null;
			_variableHandles.Reset();
			_frameHandles.Reset();
		}

		private Variable CreateVariable(ObjectValue v)
		{
			var pname = String.Format("{0} {1}", v.TypeName, v.Name);
			return new Variable(pname, v.DisplayValue, v.HasChildren ? _variableHandles.Create(v.GetAllChildren()) : 0);
		}

		private bool HasMonoExtension(string path)
		{
			foreach (var e in MONO_EXTENSIONS) {
				if (path.EndsWith(e)) {
					return true;
				}
			}
			return false;
		}

		private static int getInt(dynamic container, string propertyName, int dflt = 0)
		{
			try {
				return (int)container[propertyName];
			}
			catch (Exception) {
				// ignore and return default value
			}
			return dflt;
		}

		private static string getString(dynamic args, string property, string dflt = null)
		{
			var s = (string)args[property];
			if (s == null) {
				return dflt;
			}
			s = s.Trim();
			if (s.Length == 0) {
				return dflt;
			}
			return s;
		}
	}
}

