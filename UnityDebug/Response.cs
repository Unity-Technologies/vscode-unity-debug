namespace UnityDebug
{
	public class Response : Message
	{
		public bool success;
		public dynamic refs;
		public dynamic body;
		public string command;
		public bool running;
		public string message;
		public int request_seq;

		public static Response Create(Request request, bool success, bool running, dynamic body)
		{
			return new Response { type = "response", success = success, running = running, body = body, command = request.command, message = success ? "OK" : "Failure", request_seq = request.seq };
		}

		public static Response Default(Request request)
		{
			return Create (request, true, false, null);
		}

		public static Response Failure(Request request, string message)
		{
			return new Response { type = "response", success = false, running = false, body = null, command = request.command, message = message, request_seq = request.seq };
		}
	}
}

