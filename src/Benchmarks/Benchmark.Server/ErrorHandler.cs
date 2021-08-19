using System;
using Horse.Messaging.Server;

namespace Benchmark.Server
{
    public class ErrorHandler : IErrorHandler
    {
        public void Error(string hint, Exception exception, string payload)
        {
            Console.WriteLine($"ERROR\t{hint} : {exception}");
        }
    }
}