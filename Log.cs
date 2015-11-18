using System;
using System.IO;
using System.Reflection;

namespace UnityDebug
{
	public static class Log
	{
		static readonly string logPath;

		public static bool Debug { get; set; }

		static Log ()
		{
			logPath = Path.Combine(Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location), Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly ().Location) + "-log.txt");
			File.WriteAllText (logPath, FormatMessage("Log\n---\n"));
		}

		static string FormatMessage(string message)
		{
			var time = DateTime.Now.ToString ("HH:mm:ss.ffffff");
			return String.Format ("{0}: {1}\n", time, message);
		}

		public static void DebugWrite(string message)
		{
			if (Debug)
				Write (message);
		}

		public static void Write(string message)
		{	
			var formattedMessage = FormatMessage (message);
			lock (logPath) {
				File.AppendAllText (logPath, formattedMessage);
			}
		}
	}
}

