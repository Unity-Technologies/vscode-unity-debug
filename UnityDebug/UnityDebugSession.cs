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
				int editorPort = FindUnityEditorPort ();

				if (editorPort == -1)
					return Task.FromResult (new DebugResult (8001, "Could not find Unity editor process", new {}));

				Debugger.Connect (IPAddress.Loopback, editorPort);
				return Task.FromResult (new DebugResult ());
			}

			return Task.FromResult (new DebugResult (8002, "Unknown target name '{_name}'. Did you mean 'Unity Editor'?", new { _name = name}));
		}

		public override Task<DebugResult> Disconnect()
		{
			Debugger.Disconnect();
			return Task.FromResult(new DebugResult());
		}

		int FindUnityEditorPort()
		{
			var processes =  System.Diagnostics.Process.GetProcesses ();

			if (null != processes) {
				foreach (System.Diagnostics.Process p in processes) {
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

	}
}

