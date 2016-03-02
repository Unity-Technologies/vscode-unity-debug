namespace MonoDevelop.Debugger.Soft.Unity
{
	public static class Platform
	{
		public static bool IsLinux
		{
			get { return VSCodeDebug.Utilities.IsLinux(); }
		}

		public static bool IsMac
		{
			get { return VSCodeDebug.Utilities.IsOSX(); }
		}

		public static bool IsWindows
		{
			get { return VSCodeDebug.Utilities.IsWindows(); }
		}
	}
}

