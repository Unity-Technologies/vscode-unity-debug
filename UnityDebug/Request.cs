using System;
using Newtonsoft.Json;

namespace UnityDebug
{
	public class Request : Message
	{
		public string command;
		public dynamic arguments;

		public override string ToString()
		{
			return "seq: " + seq + " type: " + type + " command: " + command + " arguments: " + arguments;
		}

		public static Request Parse(string req)
		{
			var jsonIndex = req.IndexOf ("\r\n\r\n");
			var json = req.Substring (jsonIndex + 4);

			var result = JsonConvert.DeserializeObject<Request> (json);

			return result;
		}

	}
}

