using System;

namespace UnityDebug
{
	public class Message
	{
		public int seq;
		public string type;

		[NonSerialized]
		public static int seqCounter = 0;

		public Message()
		{
		}

		public Message(string type)
		{
			this.type = type;
		}
	}
}

