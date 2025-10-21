using System;

namespace Horse.Messaging.Server.Logging;

/// <summary>
/// Exception implementations for Horse MQ
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Executed when an exception is thrown by the server
    /// </summary>
    void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception);
}