using System;
using OpenDebug;

namespace UnityDebug
{
	public static class EngineFactory
	{
		public static IDebugSession CreateDebugSession(string adapterID, Action<DebugEvent> callback)
		{
			if (adapterID == "unity")
				return new UnityDebug.UnityDebugSession (callback);

			return null;
		}
	}
}
