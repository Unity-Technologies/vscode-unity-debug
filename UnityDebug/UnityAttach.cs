using MonoDevelop.Debugger.Soft.Unity;
using System.Collections.Generic;
using System.Linq;

namespace UnityDebug
{
	public static class UnityAttach
	{
		static readonly Dictionary<string, string> targetNameToProcessName = new Dictionary<string,string> 
		{
			{ "unity editor", "Unity Editor" },
			{ "osx player", "OSXPlayer" },
			{ "windows player", "WindowsPlayer" },
			{ "linux player", "LinuxPlayer" },
			{ "ios player", "iPhonePlayer" },
			{ "android player", "AndroidPlayer" }
		};


		public static IEnumerable<UnityProcessInfo> GetAttachableProcesses (string targetName)
		{
			string processName;

			UnityProcessDiscovery.GetProcessOptions options = UnityProcessDiscovery.GetProcessOptions.All;

			if (!targetNameToProcessName.TryGetValue (targetName.ToLower (), out processName)) {
				processName = targetName;
			} else {
				if (processName == "Unity Editor")
					options = UnityProcessDiscovery.GetProcessOptions.Editor;
				else
					options = UnityProcessDiscovery.GetProcessOptions.Players;
			}

			var processes = UnityProcessDiscovery.GetAttachableProcesses (options);

			processes.ForEach (p => Log.Write ("Found Unity process: " + p.Name + " (" + p.Id + ")"));

			return processes.Where (p => p.Name.Contains (processName));
		}
	}
}

