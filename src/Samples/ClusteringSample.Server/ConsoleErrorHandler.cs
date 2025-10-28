using System;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;

namespace ClusteringSample.Server;

public class ConsoleErrorHandler : IErrorHandler
{
    public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        Console.WriteLine($"Error {message}: {exception}");
    }
}