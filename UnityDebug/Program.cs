using System;
using System.IO;
using VSCodeDebug;
using System.Linq;
using MonoDevelop.Debugger.Soft.Unity;

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

		};

		class CustomLogger : Mono.Debugging.Client.ICustomLogger
		{
			public void LogAndShowException(string message, Exception ex)
			{
				LogError(message, ex);
			}

			public void LogError(string message, Exception ex)
			{
				Log.Write(message + (ex != null ? System.Environment.NewLine + ex.ToString() : string.Empty));
			}

			public void LogMessage(string messageFormat, params object[] args)
			{
				Log.Write(String.Format(messageFormat, args));
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

		private static void RunSession(Stream inputStream, Stream outputStream)
		{
			DebugSession debugSession = new UnityDebugSession();
			Mono.Debugging.Client.DebuggerLoggingService.CustomLogger = new CustomLogger();
			debugSession.Start(inputStream, outputStream).Wait();
		}

		public static string GetUnityProcesses()
		{
			var options = UnityProcessDiscovery.GetProcessOptions.All;


			var processes = UnityProcessDiscovery.GetAttachableProcesses (options);

			return string.Join("\n", processes.Select(x => x.Name));
		}
	}
}

