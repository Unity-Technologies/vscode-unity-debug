using System;
using System.IO;
using VSCodeDebug;
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

		static void Main(string[] argv)
		{
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
			debugSession.Start(inputStream, outputStream).Wait();
		}
	}
}

