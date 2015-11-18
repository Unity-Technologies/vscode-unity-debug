using Newtonsoft.Json;

namespace UnityDebug
{
	public class Event : Message
	{
		[JsonProperty (PropertyName = "event")]
		public string eventType;

		public dynamic body;

		public Event () : base ("event")
		{
		}

		public Event (string type, dynamic body = null) : base ("event")
		{
			this.eventType = type;
			this.body = body;
		}
	}
}

