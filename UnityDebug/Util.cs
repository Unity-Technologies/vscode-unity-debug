namespace MonoDevelop.Debugger.Soft.Unity
{
	public class Util
	{
		static Util()
		{
			UnityLocation = "";
			UnityEditorDataFolder = "";
		}

		public static string UnityLocation { get; set; }

		public static string UnityEditorDataFolder { get; set; }

		public static string FindUnity ()	
		{
			return string.Empty;
		}
	}
}
