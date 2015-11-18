using System;

namespace UnityDebug
{
	public class Thread
	{
		public string name;
		public int id;

		public Thread (int id, string name)
		{
			this.id = id;
			this.name = string.IsNullOrEmpty (name) ? string.Format ("Thread #{0}", id) : name;
		}
	}
}

