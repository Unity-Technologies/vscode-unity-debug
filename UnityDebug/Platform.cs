using System;
using System.IO;

namespace MonoDevelop.Debugger.Soft.Unity
{
	public static class Platform
	{
		private const string OSASCRIPT = "/usr/bin/osascript";	// osascript is the AppleScript interpreter on OS X
		private const string LINUX_TERM = "/usr/bin/gnome-terminal";	//private const string LINUX_TERM = "/usr/bin/x-terminal-emulator";

		/*
		 * Is this Windows?
		 */
		public static bool IsWindows
		{
			get
			{
				var pid = Environment.OSVersion.Platform;
				return !(pid == PlatformID.Unix || pid == PlatformID.MacOSX);
			}
		}

		/*
		 * Is this OS X?
		 */
		public static bool IsMac => File.Exists(OSASCRIPT);

		/*
		 * Is this Linux?
		 */
		public static bool IsLinux => File.Exists(LINUX_TERM);
	}
}

