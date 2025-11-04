using Horse.Core;

namespace MessagingServer;

public class ConsoleLogger : ILogger
{
    public void LogException(string hint, Exception exception)
    {
        Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [{hint}] {exception}");
    }

    public void LogEvent(string hint, string message)
    {
        Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [{hint}] {message}");
    }
}