/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Copyright (c) Unity Technologies.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Net;
using Mono.Debugging.Client;
using VSCodeDebug;
using MonoDevelop.Debugger.Soft.Unity;
using MonoDevelop.Unity.Debugger;
using Newtonsoft.Json.Linq;
using Breakpoint = Mono.Debugging.Client.Breakpoint;
using StackFrame = Mono.Debugging.Client.StackFrame;
using UnityProcessInfo = MonoDevelop.Debugger.Soft.Unity.UnityProcessInfo;

namespace UnityDebug
{
	internal class UnityDebugSession : DebugSession
	{

		private const string MONO = "mono";
		private readonly string[] MONO_EXTENSIONS = new String[] {
			".cs", ".csx",
			".cake",
			".fs", ".fsi", ".ml", ".mli", ".fsx", ".fsscript",
			".hx"
		};
		private const int MAX_CHILDREN = 100;
		private const int MAX_CONNECTION_ATTEMPTS = 10;
		private const int CONNECTION_ATTEMPT_INTERVAL = 500;

		private AutoResetEvent _resumeEvent = new AutoResetEvent(false);
		private bool _debuggeeExecuting = false;
		private readonly object _lock = new object();
		private Mono.Debugging.Soft.SoftDebuggerSession _session;
		private volatile bool _debuggeeKilled = true;
		private ProcessInfo _activeProcess;
		private Mono.Debugging.Client.StackFrame _activeFrame;
		SourceBreakpoint[] breakpoints = new SourceBreakpoint[0];
		private List<Catchpoint> _catchpoints;
		private DebuggerSessionOptions _debuggerSessionOptions;

		private System.Diagnostics.Process _process;
		private Handles<ObjectValue[]> _variableHandles;
		private Handles<Mono.Debugging.Client.StackFrame> _frameHandles;
		private ObjectValue _exception;
		private Dictionary<int, VSCodeDebug.Thread> _seenThreads = new Dictionary<int, VSCodeDebug.Thread>();
		private bool _attachMode = false;
		private bool _terminated = false;
		private bool _stderrEOF = true;
		private bool _stdoutEOF = true;

		IUnityDbgConnector unityDebugConnector;
			
		public UnityDebugSession() : base()
		{
			_variableHandles = new Handles<ObjectValue[]>();
			_frameHandles = new Handles<Mono.Debugging.Client.StackFrame>();
			_seenThreads = new Dictionary<int, VSCodeDebug.Thread>();

			_debuggerSessionOptions = new DebuggerSessionOptions {
				EvaluationOptions = EvaluationOptions.DefaultOptions
			};

			_session = new UnityDebuggerSession();
			_session.Breakpoints = new BreakpointStore();

			_catchpoints = new List<Catchpoint>();

			DebuggerLoggingService.CustomLogger = new CustomLogger();

			_session.ExceptionHandler = ex => {
				return true;
			};

			_session.LogWriter = (isStdErr, text) => {
			};

			_session.TargetStopped += (sender, e) => {
				if (e.Backtrace != null) {
					Frame = e.Backtrace.GetFrame (0);
				} else {
					SendOutput("stdout", "e.Bracktrace is null");
				}
				Stopped();
				SendEvent(CreateStoppedEvent("step", e.Thread));
				_resumeEvent.Set();
			};

			_session.TargetHitBreakpoint += (sender, e) => {
				Frame = e.Backtrace.GetFrame(0);
				Stopped();
				SendEvent(CreateStoppedEvent("breakpoint", e.Thread));
				_resumeEvent.Set();
			};

			_session.TargetExceptionThrown += (sender, e) => {
				Frame = e.Backtrace.GetFrame (0);
				for (var i = 0; i < e.Backtrace.FrameCount; i++) {
					if (!e.Backtrace.GetFrame (i).IsExternalCode) {
						Frame = e.Backtrace.GetFrame (i);
						break;
					}
				}
				Stopped();
				var ex = DebuggerActiveException();
				if (ex != null) {
					_exception = ex.Instance;
					SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
				}
				_resumeEvent.Set();
			};

			_session.TargetUnhandledException += (sender, e) => {
				Stopped ();
				var ex = DebuggerActiveException();
				if (ex != null) {
					_exception = ex.Instance;
					SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
				}
				_resumeEvent.Set();
			};

			_session.TargetStarted += (sender, e) => {
				_activeFrame = null;
			};

			_session.TargetReady += (sender, e) => {
				_activeProcess = _session.GetProcesses().SingleOrDefault();
			};

			_session.TargetExited += (sender, e) => {

				DebuggerKill();

				_debuggeeKilled = true;

				Terminate("target exited");

				_resumeEvent.Set();
			};

			_session.TargetInterrupted += (sender, e) => {
				_resumeEvent.Set();
			};

			_session.TargetEvent += (sender, e) => {
			};

			_session.TargetThreadStarted += (sender, e) => {
				int tid = (int)e.Thread.Id;
				lock (_seenThreads) {
					_seenThreads[tid] = new VSCodeDebug.Thread(tid, e.Thread.Name);
				}
				SendEvent(new ThreadEvent("started", tid));
			};

			_session.TargetThreadStopped += (sender, e) => {
				int tid = (int)e.Thread.Id;
				lock (_seenThreads) {
					_seenThreads.Remove(tid);
				}
				SendEvent(new ThreadEvent("exited", tid));
			};

			_session.OutputWriter = (isStdErr, text) => {
				SendOutput(isStdErr ? "stderr" : "stdout", text);
			};
		}

		public StackFrame Frame { get; set; }

		public override void Initialize(Response response, dynamic args)
		{
			var os = Environment.OSVersion;
			if (os.Platform != PlatformID.MacOSX && os.Platform != PlatformID.Unix && os.Platform != PlatformID.Win32NT) {
				SendErrorResponse(response, 3000, "Mono Debug is not supported on this platform ({_platform}).", new { _platform = os.Platform.ToString() }, true, true);
				return;
			}

			SendOutput("stdout", "UnityDebug: Initializing");

			SendResponse(response, new Capabilities() {
				// This debug adapter does not need the configurationDoneRequest.
				supportsConfigurationDoneRequest = false,

				// This debug adapter does not support function breakpoints.
				supportsFunctionBreakpoints = true,

				// This debug adapter doesn't support conditional breakpoints.
				supportsConditionalBreakpoints = true,

				// This debug adapter does support a side effect free evaluate request for data hovers.
				supportsEvaluateForHovers = true,

				supportsExceptionOptions = true,

				supportsHitConditionalBreakpoints = true,

				supportsSetVariable = true,

				// This debug adapter does not support exception breakpoint filters
				exceptionBreakpointFilters = new[] { new ExceptionBreakpointsFilter("all_exceptions", "All Exceptions"),}
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

			SetExceptionBreakpoints(args.__exceptionOptions);

			SendOutput("stdout", "UnityDebug: Searching for Unity process '" + name + "'");

			var processes = UnityAttach.GetAttachableProcesses(name).ToArray ();

			if (processes.Length == 0) {
				SendErrorResponse (response, 8001, "Could not find target name '{_name}'. Is it running?", new { _name = name});
				return;
			}

			UnityProcessInfo process;
			if (processes.Length == 1)
			{
				process = processes[0];
			}
			else
			{
				if (name.Contains("Editor"))
				{
					string pathToEditorInstanceJson = getString(args, "path");
					var jObject = JObject.Parse(File.ReadAllText(pathToEditorInstanceJson));
					var processId = jObject["process_id"].ToObject<int>();
					process = processes.First(p => p.Id == processId);
				}
				else
				{
					SendErrorResponse(response, 8002, "Multiple targets with name '{_name}' running. Unable to connect.\n" +
						"Use \"Unity Attach Debugger\" from the command palette (View > Command Palette...) to specify which process to attach to.", new { _name = name });

					SendOutput("stdout", "UnityDebug: Multiple targets with name '" + name + "' running. Unable to connect.\n" +
						"Use \"Unity Attach Debugger\" from the command palette (View > Command Palette...) to specify which process to attach to.");

					foreach (var p in processes)
						SendOutput("stdout", "UnityDebug: Found Unity process '" + p.Name + "' (" + p.Id + ")\n");

					return;
				}
			}

			var attachInfo = UnityProcessDiscovery.GetUnityAttachInfo (process.Id, ref unityDebugConnector);

			Connect(attachInfo.Address, attachInfo.Port);

			SendOutput("stdout", "UnityDebug: Attached to Unity process '" + process.Name + "' (" + process.Id + ")\n");
			SendResponse(response);
		}

		private void Connect(IPAddress address, int port)
		{
			lock (_lock) {

				_debuggeeKilled = false;

				var args0 = new Mono.Debugging.Soft.SoftDebuggerConnectArgs(string.Empty, address, port) {
					MaxConnectionAttempts = MAX_CONNECTION_ATTEMPTS,
					TimeBetweenConnectionAttempts = CONNECTION_ATTEMPT_INTERVAL
				};

				_session.Run(new Mono.Debugging.Soft.SoftDebuggerStartInfo(args0), _debuggerSessionOptions);

				_debuggeeExecuting = true;
			}
		}

		//---- private ------------------------------------------
		private void SetExceptionBreakpoints(dynamic exceptionOptions)
		{
			if (exceptionOptions != null) {

				// clear all existig catchpoints
				foreach (var cp in _catchpoints) {
					_session.Breakpoints.Remove(cp);
				}
				_catchpoints.Clear();

				var exceptions = exceptionOptions.ToObject<dynamic[]>();
				for (int i = 0; i < exceptions.Length; i++) {

					var exception = exceptions[i];

					string exName = null;
					string exBreakMode = exception.breakMode;

					if (exception.path != null) {
						var paths = exception.path.ToObject<dynamic[]>();
						var path = paths[0];
						if (path.names != null) {
							var names = path.names.ToObject<dynamic[]>();
							if (names.Length > 0) {
								exName = names[0];
							}
						}
					}

					if (exName != null && exBreakMode == "always") {
						_catchpoints.Add(_session.Breakpoints.AddCatchpoint(exName));
					}
				}
			}
		}

		public override void Disconnect(Response response, dynamic args)
		{
			if (unityDebugConnector != null) {
				unityDebugConnector.OnDisconnect ();
				unityDebugConnector = null;
			}

			lock (_lock) {
				if (_session != null) {
					_debuggeeExecuting = true;
					breakpoints = null;
					_session.Breakpoints.Clear();
					_session.Continue();
					_session.Detach();
					_session.Adaptor.Dispose();
					_session = null;
				}
			}

			SendOutput("stdout", "UnityDebug: Disconnected");
			SendResponse(response);
		}

		public override void Continue(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session == null || _session.IsRunning || _session.HasExited) return;

				_session.Continue();
				_debuggeeExecuting = true;
			}
		}

		private void WaitForSuspend()
		{
			if (!_debuggeeExecuting) return;

			_resumeEvent.WaitOne();
			_debuggeeExecuting = false;
		}

		public override void Next(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session == null || _session.IsRunning || _session.HasExited) return;

				_session.NextLine();
				_debuggeeExecuting = true;
			}
		}

		public override void StepIn(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session == null || _session.IsRunning || _session.HasExited) return;

				_session.StepLine();
				_debuggeeExecuting = true;
			}
		}

		public override void StepOut(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session == null || _session.IsRunning || _session.HasExited) return;

				_session.Finish();
				_debuggeeExecuting = true;
			}
		}

		public override void Pause(Response response, dynamic args)
		{
			SendResponse(response);
			PauseDebugger();
		}

		private void PauseDebugger()
		{
			lock (_lock) {
				if (_session != null && _session.IsRunning)
					_session.Stop();
			}
		}

		protected override void SetVariable(Response response, object args)
		{
			SendOutput("stdout", "Found this: " + args);
			SendOutput("stdout", "This is response: " + response);
			int reference = getInt(args, "variablesReference", -1);
			if (reference == -1) {
				SendErrorResponse(response, 3009, "variables: property 'variablesReference' is missing", null, false, true);
				return;
			}

			string value = getString(args, "value");
			string type = "";
			if (_variableHandles.TryGet(reference, out var children)) {
				if (children != null && children.Length > 0) {
					if (children.Length > MAX_CHILDREN) {
						children = children.Take(MAX_CHILDREN).ToArray();
					}

					foreach (var v in children) {
						if (v.IsError)
							continue;
						v.WaitHandle.WaitOne();
						var variable = CreateVariable(v);
						if (variable.name == getString(args, "name"))
						{
							v.Value = value;
							SendResponse(response, new SetVariablesResponseBody(value, variable.type, variable.variablesReference));
						}
					}
				}
			}
		}

		public override void SetExceptionBreakpoints(Response response, dynamic args)
		{
			/*if (args["filters"].Contains("all_exceptions"))
			{
				_session.TargetExceptionThrown
			}*/
			SetExceptionBreakpoints(args.exceptionOptions);
			SendResponse(response);
		}

		public override void SetBreakpoints(Response response, dynamic args)
		{
			string path = null;
			if (args.source != null) {
				var p = (string)args.source.path;
				if (p != null && p.Trim().Length > 0) {
					path = p;
				}
			}
			if (path == null) {
				SendErrorResponse(response, 3010, "setBreakpoints: property 'source' is empty or misformed", null, false, true);
				return;
			}

			if (!HasMonoExtension(path)) {
				// we only support breakpoints in files mono can handle
				SendResponse(response, new SetBreakpointsResponseBody());
				return;
			}

			SourceBreakpoint[] newBreakpoints = getBreakpoints(args, "breakpoints");
			var lines = newBreakpoints.Select(bp => bp.line);
			var breakpointsToRemove = breakpoints.Where(bp => !lines.Contains(bp.line)).ToArray();
			foreach (Breakpoint breakpoint in _session.Breakpoints.GetBreakpoints())
			{
				if (breakpointsToRemove.Any(bp => bp.line == breakpoint.Line))
				{
					_session.Breakpoints.Remove(breakpoint);
				}
			}

			foreach (var sourceBreakpoint in newBreakpoints)
			{
				var breakEvents = _session.Breakpoints.Where(bp => ((Breakpoint)bp).Line == sourceBreakpoint.line);
				var enumerable = breakEvents as BreakEvent[] ?? breakEvents.ToArray();
				if (enumerable.Any())
				{
					enumerable.First().ConditionExpression = sourceBreakpoint.condition;
				}
				else
				{
					var breakpoint = _session.Breakpoints.Add(path, sourceBreakpoint.line);
					breakpoint.ConditionExpression = sourceBreakpoint.condition;
				}
			}

			breakpoints = newBreakpoints;

			var responseBreakpoints = newBreakpoints.Select(sourceBreakpoint =>
				new VSCodeDebug.Breakpoint(true, sourceBreakpoint.line, sourceBreakpoint.column, sourceBreakpoint.logMessage)).ToList();
			SendResponse(response, new SetBreakpointsResponseBody(responseBreakpoints));
		}

		public override void StackTrace(Response response, dynamic args)
		{
			int maxLevels = getInt(args, "levels", 10);
			int threadReference = getInt(args, "threadId", 0);

			WaitForSuspend();

			ThreadInfo thread = DebuggerActiveThread();
			if (thread.Id != threadReference) {
				// Console.Error.WriteLine("stackTrace: unexpected: active thread should be the one requested");
				thread = FindThread(threadReference);
				if (thread != null) {
					thread.SetActive();
				}
			}

			var stackFrames = new List<VSCodeDebug.StackFrame>();
			int totalFrames = 0;

			var bt = thread.Backtrace;
			if (bt != null && bt.FrameCount >= 0) {

				totalFrames = bt.FrameCount;

				for (var i = 0; i < Math.Min(totalFrames, maxLevels); i++) {

					var frame = bt.GetFrame(i);

					string path = frame.SourceLocation.FileName;

					var hint = "subtle";
					Source source = null;
					if (!string.IsNullOrEmpty(path)) {
						string sourceName = Path.GetFileName(path);
						if (!string.IsNullOrEmpty(sourceName)) {
							if (File.Exists(path)) {
								source = new Source(sourceName, ConvertDebuggerPathToClient(path), 0, "normal");
								hint = "normal";
							} else {
								source = new Source(sourceName, null, 1000, "deemphasize");
							}
						}
					}

					var frameHandle = _frameHandles.Create(frame);
					string name = frame.SourceLocation.MethodName;
					int line = frame.SourceLocation.Line;
					stackFrames.Add(new VSCodeDebug.StackFrame(frameHandle, name, source, ConvertDebuggerLineToClient(line), 0, hint));
				}
			}

			SendResponse(response, new StackTraceResponseBody(stackFrames, totalFrames));
		}

		private ThreadInfo DebuggerActiveThread()
		{
			lock (_lock) {
				return _session == null ? null : _session.ActiveThread;
			}
		}

		public override void Source(Response response, dynamic arguments) {
			SendErrorResponse(response, 1020, "No source available");
		}

		public override void Scopes(Response response, dynamic args) {

			int frameId = getInt(args, "frameId", 0);
			var frame = _frameHandles.Get(frameId, null);

			var scopes = new List<Scope>();

			if (frame.Index == 0 && _exception != null) {
				scopes.Add(new Scope("Exception", _variableHandles.Create(new ObjectValue[] { _exception })));
			}

			var locals = new[] { frame.GetThisReference() }.Concat(frame.GetParameters()).Concat(frame.GetLocalVariables()).Where(x => x != null).ToArray();
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

			WaitForSuspend();
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
						variables.Add(new Variable("...", null, null));
					}
				}
			}

			SendResponse(response, new VariablesResponseBody(variables));
		}

		public override void Threads(Response response, dynamic args)
		{
			var threads = new List<VSCodeDebug.Thread>();
			var process = _activeProcess;
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
			SendOutput("stdout", "Starting");
			var expression = getString(args, "expression");

			if (expression == null) {
				SendError(response, "expression missing");
				return;
			}
			if (Frame == null)
			{
				SendError(response, "no active stackframe");
				return;
			}
			if (!Frame.ValidateExpression(expression))
			{
				SendError(response, "invalid expression");
				return;
			}
			SendOutput("stdout", "Valid expression " + args);

			var evaluationOptions = _debuggerSessionOptions.EvaluationOptions.Clone();
			evaluationOptions.EllipsizeStrings = false;
			evaluationOptions.AllowMethodEvaluation = true;
			_session.Options.EvaluationOptions = evaluationOptions;
			_session.Options.ProjectAssembliesOnly = true;
			_session.Options.StepOverPropertiesAndOperators = false;
			var val = Frame.GetExpressionValue(expression, true);
			SendOutput("stdout", "Sent expression");
			val.WaitHandle.WaitOne();
			SendOutput("stdout", "Waiting");

			var flags = val.Flags;
			if (flags.HasFlag(ObjectValueFlags.Error) || flags.HasFlag(ObjectValueFlags.NotSupported))
			{
				string error = val.DisplayValue;
				if (error.IndexOf("reference not available in the current evaluation context") > 0)
				{
					error = "not available";
				}
				SendError(response, error);
				return;
			}
			if (flags.HasFlag(ObjectValueFlags.Unknown))
			{
				SendError(response, "invalid expression");
				return;
			}
			if (flags.HasFlag(ObjectValueFlags.Object) && flags.HasFlag(ObjectValueFlags.Namespace))
			{
				SendError(response, "not available");
				return;
			}
			
			int handle = 0;
			if (val.HasChildren)
			{
				handle = _variableHandles.Create(val.GetAllChildren());
			}

			SendResponse(response, new EvaluateResponseBody(val.DisplayValue, handle));
		}

		void SendError(Response response, string error)
		{
			SendErrorResponse(response, 3014, "Evaluate request failed ({_reason}).", new { _reason = error });
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

				// wait until we've seen the end of stdout and stderr
				for (int i = 0; i < 100 && (_stdoutEOF == false || _stderrEOF == false); i++) {
					System.Threading.Thread.Sleep(100);
				}

				SendEvent(new TerminatedEvent());

				_terminated = true;
				_process = null;
			}
		}

		private StoppedEvent CreateStoppedEvent(string reason, ThreadInfo ti, string text = null)
		{
			return new StoppedEvent((int)ti.Id, reason, text);
		}

		private ThreadInfo FindThread(int threadReference)
		{
			if (_activeProcess != null) {
				foreach (var t in _activeProcess.GetThreads()) {
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

		/*private Variable CreateVariable(ObjectValue v)
		{
			var pname = String.Format("{0} {1}", v.TypeName, v.Name);
			return new Variable(pname, v.DisplayValue, v.HasChildren ? _variableHandles.Create(v.GetAllChildren()) : 0);
		}*/

		private Variable CreateVariable(ObjectValue v)
		{
			var dv = v.DisplayValue;
			if (dv.Length > 1 && dv [0] == '{' && dv [dv.Length - 1] == '}') {
				dv = dv.Substring (1, dv.Length - 2);
			}
			return new Variable(v.Name, dv, v.TypeName, v.HasChildren ? _variableHandles.Create(v.GetAllChildren()) : 0);
		}

		private Backtrace DebuggerActiveBacktrace() {
			var thr = DebuggerActiveThread();
			return thr == null ? null : thr.Backtrace;
		}

		private ExceptionInfo DebuggerActiveException() {
			var bt = DebuggerActiveBacktrace();
			return bt == null ? null : bt.GetFrame(0).GetException();
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

		private static bool getBool(dynamic container, string propertyName, bool dflt = false)
		{
			try {
				return (bool)container[propertyName];
			}
			catch (Exception) {
				// ignore and return default value
			}
			return dflt;
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

		static SourceBreakpoint[] getBreakpoints(dynamic args, string property)
		{
			JArray jsonBreakpoints = args[property];
			var breakpoints = jsonBreakpoints.ToObject<SourceBreakpoint[]>();
			return breakpoints ?? new SourceBreakpoint[0];
		}
		
		private void DebuggerKill()
		{
			lock (_lock) {
				if (_session != null) {

					_debuggeeExecuting = true;

					if (!_session.HasExited)
						_session.Exit();

					_session.Dispose();
					_session = null;
				}
			}
		}
	}
}

