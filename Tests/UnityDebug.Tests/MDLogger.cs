using System;
using Mono.Debugging.Client;

namespace UnityDebug.Tests
{
    public class MDLogger : ICustomLogger
    {
        public string GetNewDebuggerLogFilename ()
        {
            return "";
        }

        public void LogError (string message, Exception ex)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine(ex.Message);
        }

        public void LogAndShowException (string message, Exception ex)
        {
            LogError(message, ex);
        }

        public void LogMessage (string messageFormat, params object[] args)
        {
            Console.Out.WriteLine(messageFormat, args);
        }
    }
}
