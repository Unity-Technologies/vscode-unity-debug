using System;
using System.Net;
using System.Threading.Tasks;
using Mono.Debugger.Client;
using Mono.Debugging.Client;
using OpenDebug;

namespace UnityDebug
{
	internal class UnityDebugSession : SDBDebugSession
	{
		public UnityDebugSession(Action<DebugEvent> callback) : base(callback)
		{
			VariablesIgnoreFlags = ObjectValueFlags.Error;
		}

		public override Task<DebugResult> Launch(dynamic args)
		{
			return Attach (args);
		}

		public override Task<DebugResult> Attach(dynamic args)
		{
			string name = getString (args, "name");
			var nameLower = name.ToLower ();

			if (nameLower.Contains ("unity") && nameLower.Contains ("editor")) {
				var editorProcess = FindUnityEditorProcess ();

				if (editorProcess == null)
					return Task.FromResult (new DebugResult (8001, "Could not find Unity editor process", new {}));

				Debugger.Connect (IPAddress.Loopback, GetDebuggerPort(editorProcess));

				var debugResult = new DebugResult ();
				debugResult.Add(new OutputEvent("UnityDebug: Attached to Unity editor process '" + editorProcess.ProcessName + "' (" + editorProcess.Id + ")\n"));

				return Task.FromResult (debugResult);
			}

			return Task.FromResult (new DebugResult (8002, "Unknown target name '{_name}'. Did you mean 'Unity Editor'?", new { _name = name}));
		}

		public override Task<DebugResult> Disconnect()
		{
			Debugger.Disconnect();
			return Task.FromResult(new DebugResult());
		}

		System.Diagnostics.Process FindUnityEditorProcess()
		{
			var processes = System.Diagnostics.Process.GetProcesses ();

			if (null != processes) {
				foreach (System.Diagnostics.Process p in processes) {
					try {
						if ((p.ProcessName.StartsWith ("unity", StringComparison.OrdinalIgnoreCase) ||
							p.ProcessName.Contains ("Unity.app")) &&
							!p.ProcessName.Contains ("UnityDebug") &&
							!p.ProcessName.Contains ("UnityShader") &&
							!p.ProcessName.Contains ("UnityHelper") &&
							!p.ProcessName.Contains ("Unity Helper")) 
						{
							return p;
						}
					} catch {
						// Don't care; continue
					}
				}
			}

			return null;
		}

		int GetDebuggerPort(System.Diagnostics.Process p)
		{
			return 56000 + (p.Id % 1000);
		}
		
	}
}

