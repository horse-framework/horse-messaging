using System;
using Horse.Core;

namespace ClusteringSample.Server
{
    public class ConsoleLogger : ILogger
    {
        public void LogException(string hint, Exception exception)
        {
            Console.WriteLine("ERROR: " + hint + " - " + exception);
        }

        public void LogEvent(string hint, string message)
        {
            Console.WriteLine(hint + "\t" + message);
        }
    }
}