using System;
using System.Reflection;

namespace UnityDebug
{
	class UnityDebug
	{
		public static void Main (string[] args)
		{
			Log.Write ("Starting " + Assembly.GetExecutingAssembly().GetName().Name);
			Log.Write ("Arguments: '" + string.Join (" ", args) + "'");
		}
	}
}
