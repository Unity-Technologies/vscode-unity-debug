using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Debugging.Client;
using MonoDevelop.Debugger.Soft.Unity;
using VSCodeDebug;

namespace UnityDebug
{
	public class Program
	{
		class Logger : MonoDevelop.Debugger.Soft.Unity.Log.ILogger
		{
			public void Info(string message)
			{
				Log.Write (message);
			}

			public void Warning(string message, Exception e)
			{
				Log.Write (message);
			}

			public void Error(string message, Exception e)
			{
				Log.Write (message);
			}

		}

		class CustomLogger : ICustomLogger
		{
			public void LogAndShowException(string message, Exception ex)
			{
				LogError(message, ex);
			}

			public void LogError(string message, Exception ex)
			{
				Log.LogError(message, ex);
			}

			public void LogMessage(string messageFormat, params object[] args)
			{
				Log.Write(string.Format(messageFormat, args));
			}

			public string GetNewDebuggerLogFilename()
			{
				return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) + "-another-log.txt");
			}
		}

		static void Main(string[] argv)
		{
			if(argv.Length > 0 && argv[0] == "list")
			{
				Console.Write(GetUnityProcesses());
				return;
			}

			Log.Write ("UnityDebug");

			MonoDevelop.Debugger.Soft.Unity.Log.AddLogger (new Logger());

			try
			{
				RunSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
			}
			catch(Exception e)
			{
				Log.Write ("Exception: " + e);
			}
		}

		static void RunSession(Stream inputStream, Stream outputStream)
		{
			Log.Write("Running session");
			DebugSession debugSession = new UnityDebugSession();
			DebuggerLoggingService.CustomLogger = new CustomLogger();
			debugSession.Start(inputStream, outputStream).Wait();
			Log.Write("Session Terminated");
		}

		static string GetUnityProcesses()
		{
			var processes = UnityProcessDiscovery.GetAttachableProcesses();

			return string.Join("\n", processes.Select(x => x.Name + $" ({x.Id})"));
		}
	}
}

