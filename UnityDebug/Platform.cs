namespace MonoDevelop.Debugger.Soft.Unity
{
	public static class Platform
	{
		public static bool IsLinux
		{
			get { return OpenDebug.Utilities.IsLinux(); }
		}

		public static bool IsMac
		{
			get { return OpenDebug.Utilities.IsOSX(); }
		}

		public static bool IsWindows
		{
			get { return OpenDebug.Utilities.IsWindows(); }
		}
	}
}

