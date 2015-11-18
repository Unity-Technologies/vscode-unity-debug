using System;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityDebug
{
	public class UnityDebug
	{
		static readonly Regex ContentLengthMatcher = new Regex("Content-Length: (\\d+)");

		Stream inputStream;
		Stream outputStream;
		StreamWriter outputWriter;

		public UnityDebug (Stream inputStream, Stream outputStream)
		{
			this.inputStream = inputStream;
			this.outputStream = outputStream;

			outputWriter = new StreamWriter (outputStream);
		}

		public void Run()
		{
			byte[] buffer = new byte[4096];
			string requestHead = string.Empty;

			while (true) 
			{
				Log.DebugWrite ("Reading StandardInput. requestHead: '" + ConvertNewlinesToEscapeSequence(requestHead) +"'");

				var requestString = string.Empty;

				if (requestHead.Length == 0) 
				{
					int numBytes = inputStream.Read (buffer, 0, buffer.Length);

					if (numBytes == 0) {
						Log.Write ("StandardInput empty. Terminating");
						break;
					}

					requestString = requestHead + Settings.Encoding.GetString (buffer, 0, numBytes);
				} 
				else 
				{
					requestString = requestHead;
				}

				requestHead = string.Empty;
				var match = ContentLengthMatcher.Match (requestString);

				if (match.Success) 
				{
					var singleRequest = string.Empty;

					int contentLength = Int32.Parse(match.Groups [1].Value);
					int requestIndex = requestString.IndexOf ("\r\n\r\n") + 4;

					if (requestString.Length < requestIndex + contentLength) 
					{
						Log.DebugWrite ("Read " + requestString.Length + " Expecting " + (requestIndex + contentLength));

						int numBytes = inputStream.Read (buffer, 0, buffer.Length);
						var requestTail = Settings.Encoding.GetString (buffer, 0, numBytes);

						var fullRequest = requestString + requestTail;

						singleRequest = fullRequest.Substring (0, requestIndex + contentLength);
						requestHead = fullRequest.Substring (requestIndex + contentLength);
					} 
					else 
					{
						singleRequest = requestString.Substring (0, requestIndex + contentLength);
						requestHead = requestString.Substring (requestIndex + contentLength);
					}

					Log.Write (Program.Name + ".StandardInput  : '" + ConvertNewlinesToEscapeSequence (singleRequest) + "'");
				} 
				else
				{
					Log.Write ("No Content-Length match : '" + requestString + "'");
				}
			}
		}

		string ConvertNewlinesToEscapeSequence(string input)
		{
			return input.Replace ("\n", "\\n").Replace ("\r", "\\r");
		}

	}
}

