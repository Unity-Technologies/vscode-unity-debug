using System;
using Newtonsoft.Json;
using Mono.Debugging.Soft;
using Mono.Debugging.Client;
using System.Net;
using System.Diagnostics;

namespace UnityDebug
{
	public class UnityDebugSession
	{
		SoftDebuggerSession session;
		bool initialized = false;

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
					string target = request.arguments.name;
					var errorMessage = Connect (target);
					return errorMessage != null ? Response.Failure (request, errorMessage) : Response.Default (request);

				case "disconnect":
					Disconnect ();
					return Response.Default (request);

				default:
					Log.Write (">>> ERROR: Unhandled request: " + request.command);
					return Response.Failure (request, "Unhandled request: '" + request.command + "'");
			}
		}

		void SendEvent(string type)
		{
			Log.DebugWrite ("Sending event: '" + type + "'");

			if(sendEventEvent != null)
				sendEventEvent(new Event(type));
		}

		bool Initialize(string adapterID, string pathFormat, bool startAt1)
		{
			if (adapterID != "unity")
				return false;

			pathFormatPath = (pathFormat == "path");
			linesStartAt1 = startAt1;

			session = new SoftDebuggerSession ();

			session.TargetEvent += (sender, e) => Log.Write ("Debugger Event: " + e.Type);
			session.LogWriter = (isStdErr, text) => Log.Write ("Debugger Log: " + text);
			session.OutputWriter = (isStdErr, text) => Log.Write ("Debugger Output: " + text);

			session.ExceptionHandler += ExceptionHandler;

			return true;
		}

		private string Connect(string target)
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

