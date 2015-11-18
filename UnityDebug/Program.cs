using System;
using System.Reflection;
using System.Linq;

namespace UnityDebug
{
	class Program
	{
		public static readonly string Name = Assembly.GetExecutingAssembly().GetName().Name;

		public static void Main (string[] args)
		{
			Log.Debug = args.Contains("--unity-debug-extra-logging");
			Log.Write ("Starting " + Name);
			Log.Write ("Arguments: '" + string.Join (" ", args) + "'");

			try
			{
				var unityDebug = new UnityDebug (Console.OpenStandardInput (), Console.OpenStandardOutput ());
				unityDebug.Run ();
			}
			catch(Exception e) 
			{
				Log.Write (e.ToString ());
				Environment.Exit (-1);
			}

		}
	}
}
