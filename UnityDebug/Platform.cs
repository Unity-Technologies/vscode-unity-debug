using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

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
		
		public static bool IsLocal(string address)
		{
			try
			{
				if (address == "127.0.0.1")
					return true;
				return Dns.GetHostAddresses(Dns.GetHostName()).Any(ip => ip.ToString() == address);
			}
			catch
			{
				return false;
			}
		}
	}
}

