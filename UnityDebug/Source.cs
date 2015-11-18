using System.IO;

namespace UnityDebug
{
	public class Source
	{
		public string name;
		public string path;
		public int reference;

		public Source (string name, string path, int rf = 0)
		{
			this.name = name;
			this.path = path;
			this.reference = rf;
		}

		public Source (string path, int rf = 0) : this(Path.GetFileName (path), path, rf)
		{
		}
	}
}

