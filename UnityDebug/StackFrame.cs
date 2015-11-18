using System;

namespace UnityDebug
{
	public class StackFrame
	{
		public int id;
		public string name;

		public Source source;
		public int line;
		public int column;

		public StackFrame(int id, string name, Source source, int line, int column)
		{
			this.id = id;
			this.name = name;
			this.source = source;
			this.line = line;
			this.column = column;
		}
	}
}

