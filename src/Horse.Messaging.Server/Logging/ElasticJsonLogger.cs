using System;
using System.Text.Json;

namespace Horse.Messaging.Server.Logging;

/// <summary>
/// Writes logs to console in Elastic JSON format
/// </summary>
public static class ElasticJsonConsoleLogger
{
    /// <summary>
    /// Writes log to console in Elastic JSON format.
    /// </summary>
    public static void Log(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        var logObject = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            level = logLevel.ToString(),
            logger = "Horse.Messaging.Server",
            message,
            exception = exception?.ToString(),
            eventId
        };

        var json = JsonSerializer.Serialize(logObject);
        Console.WriteLine(json);
    }
}