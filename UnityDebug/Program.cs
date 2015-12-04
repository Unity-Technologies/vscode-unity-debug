using System;
using System.IO;
using OpenDebug;

namespace UnityDebug
{
	public class Program
	{
		static void Main(string[] argv)
		{
			Dispatch(Console.OpenStandardInput(), Console.OpenStandardOutput());
		}

		static void Dispatch(Stream inputStream, Stream outputStream)
		{
			V8ServerProtocol protocol = new V8ServerProtocol(inputStream, outputStream);

			protocol.TRACE = false;
			protocol.TRACE_RESPONSE = false;

			IDebugSession debugSession = null;

			var r = protocol.Start((string command, dynamic args, IResponder responder) => {

				if (args == null) {
					args = new { };
				}

				if (command == "initialize") {
					string adapterID = Utilities.GetString(args, "adapterID");
					if (adapterID == null) {
						responder.SetBody(new ErrorResponseBody(new Message(1101, "initialize: property 'adapterID' is missing or empty")));
						return;
					}

					debugSession = EngineFactory.CreateDebugSession(adapterID, (e) => protocol.SendEvent(e.type, e));
					if (debugSession == null) {
						responder.SetBody(new ErrorResponseBody(new Message(1103, "initialize: can't create debug session for adapter '{_id}'", new { _id = adapterID })));
						return;
					}
				}

				if (debugSession != null) {

					try {
						DebugResult dr = debugSession.Dispatch(command, args);
						if (dr != null) {
							responder.SetBody(dr.Body);

							if (dr.Events != null) {
								foreach (var e in dr.Events) {
									responder.AddEvent(e.type, e);
								}
							}
						}
					}
					catch (Exception e) {
						responder.SetBody(new ErrorResponseBody(new Message(1104, "error while processing request '{_request}' (exception: {_exception})", new { _request = command, _exception = e.Message })));
					}

					if (command == "disconnect") {
						protocol.Stop();
					}
				}

			}).Result;
		}
	}
}

