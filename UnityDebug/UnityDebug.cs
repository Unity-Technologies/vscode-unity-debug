using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace UnityDebug
{
	public class UnityDebug
	{
		static readonly Regex ContentLengthMatcher = new Regex("Content-Length: (\\d+)");

		UnityDebugSession unityDebugSession = new UnityDebugSession();

		Stream inputStream;
		Stream outputStream;

		public UnityDebug (Stream inputStream, Stream outputStream)
		{
			this.inputStream = inputStream;
			this.outputStream = outputStream;

			unityDebugSession.OnSendEvent += WriteStandardOutput;
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

					Log.Write ("StandardInput  : '" + ConvertNewlinesToEscapeSequence (singleRequest) + "'");

					var request = Request.Parse (singleRequest);
					var response = unityDebugSession.HandleRequest (request);

					WriteStandardOutput (response);
				} 
				else
				{
					Log.Write ("No Content-Length match : '" + requestString + "'");
				}
			}
		}

		public void WriteStandardOutput(Message message)
		{
			WriteStandardOutput (ConvertToBytes (message));
		}

		public void WriteStandardOutput(byte[] bytes)
		{
			if (bytes == null) 
			{
				Log.Write ("StandardOutput : bytes is null");
				return;
			}

			Log.Write ("StandardOutput : '" + ConvertNewlinesToEscapeSequence(Settings.Encoding.GetString(bytes)) + "'");
			outputStream.Write(bytes, 0, bytes.Length);
			outputStream.Flush ();
		}

		static byte[] ConvertToBytes (Message message)
		{
			message.seq = ++Message.seqCounter;

			string s = JsonConvert.SerializeObject (message);
			byte[] bytes = Settings.Encoding.GetBytes (s);
			string s2 = string.Format ("Content-Length: {0}{1}", bytes.Length, "\r\n\r\n");
			byte[] bytes2 = Settings.Encoding.GetBytes (s2);
			byte[] array = new byte[bytes2.Length + bytes.Length];
			Buffer.BlockCopy (bytes2, 0, array, 0, bytes2.Length);
			Buffer.BlockCopy (bytes, 0, array, bytes2.Length, bytes.Length);
			return array;
		}
			
		string ConvertNewlinesToEscapeSequence(string input)
		{
			return input.Replace ("\n", "\\n").Replace ("\r", "\\r");
		}

	}
}

