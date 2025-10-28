using System;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;

namespace Benchmark.Server;

public class ErrorHandler : IErrorHandler
{
    public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        Console.WriteLine($"ERROR\t{message} : {exception}");
    }
}