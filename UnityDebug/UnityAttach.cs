using System;
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
			{ "osx webplayer", "OSXWebPlayer" },
			{ "windows player", "WindowsPlayer" },
			{ "windows webplayer", "WindowsWebPlayer" },
			{ "linux player", "WindowsPlayer" },
			{ "linux webplayer", "WindowsWebPlayer" },
			{ "ios player", "iPhonePlayer" },
			{ "android player", "AndroidPlayer" }
		};

		public static IEnumerable<UnityProcessInfo> GetAttachableProcesses (string targetName)
		{
			string processName;

			if (!targetNameToProcessName.TryGetValue (targetName.ToLower (), out processName))
				return null;

			var processes = UnityProcessDiscovery.GetAttachableProcesses ();

			processes.ForEach(p => Log.Write("Found Unity process: " + p.Name + " (" + p.Id + ")"));

			return processes.Where (p => p.Name.Contains (processName));
		}
	}
}

