namespace UnityDebug
{
	public class Response : Message
	{
		public int request_seq;
		public bool success;
		public string command;
		public string message;
		public dynamic body;
	
		public static Response Default(Request request)
		{
			return Success (request, null);
		}

		public static Response Success(Request request, dynamic body)
		{
			return new Response { type = "response", success = true, body = body, command = request.command, message = "OK", request_seq = request.seq };
		}

		public static Response Failure(Request request, string message)
		{
			return new Response { type = "response", success = false, body = null, command = request.command, message = message, request_seq = request.seq };
		}
	}
}

