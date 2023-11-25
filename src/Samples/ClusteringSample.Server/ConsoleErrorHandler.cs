using System;
using Horse.Messaging.Server;

namespace ClusteringSample.Server;

public class ConsoleErrorHandler : IErrorHandler
{
    public void Error(string hint, Exception exception, string payload)
    {
        Console.WriteLine($"Error {hint}: {exception}");
    }
}