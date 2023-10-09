using Horse.Core;

namespace HorseService;

public class ConsoleLogger : ILogger
{
    public void LogException(string hint, Exception exception)
    {
        Console.WriteLine($"[{hint}] {exception}");
    }

    public void LogEvent(string hint, string message)
    {
        Console.WriteLine($"[{hint}] {message}");
    }
}