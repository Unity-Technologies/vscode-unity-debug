using System;
using Newtonsoft.Json;
using Mono.Debugging.Soft;
using Mono.Debugging.Client;

namespace UnityDebug
{
	public class UnityDebugSession
	{
		SoftDebuggerSession session;
		bool initialized = false;

		static bool pathFormatPath = false;
		static bool linesStartAt1 = false;

		public Response HandleRequest(Request request)
		{
			switch (request.command) 
			{
			case "initialize":
				initialized = Initialize ((string)request.arguments.adapterID, (string)request.arguments.pathFormat, (bool)request.arguments.linesStartAt1);
	
				if (initialized) 
				{
					dynamic body = new System.Dynamic.ExpandoObject ();
					body.isReady = true;
					return Response.Create (request, true, false, body);
				} 
				else
					return Response.Failure (request, "Debugger type is " + (string)request.arguments.adapterId + " and not 'unity'");

			default:
				Log.Write (">>> ERROR: Unhandled request: " + request.command);
				return Response.Default (request);
			}
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

