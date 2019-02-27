using System;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;

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
			return $"{time}: {message}\n";
		}

		public static void DebugWrite(string message)
		{
			if (Debug)
				Write (message);
		}

		public static void LogError(string message, Exception ex)
		{
			Write(message + (ex != null ? Environment.NewLine + ex : string.Empty));
		}

		public static void Write(string message)
		{
			var formattedMessage = FormatMessage (message);
			var buf = Encoding.UTF8.GetBytes(formattedMessage);
			using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				stream.Write(buf, 0, buf.Length);
			}
		}
	}
}

