using System;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues.Exceptions;

/// <summary>
/// Description for queue consume operation exception
/// </summary>
public class ExceptionDescription
{
    /// <summary>
    /// Fullname of type of exception
    /// </summary>
    public string ExceptionType { get; set; }

    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Exception stack trace
    /// </summary>
    public string StackTrace { get; set; }

    /// <summary>
    /// Machine name
    /// </summary>
    public string MachineName { get; set; }

    /// <summary>
    /// Target name of the message (queue name, channel name, router name etc)
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Consuming client type
    /// </summary>
    public string ClientType { get; set; }

    /// <summary>
    /// Consuming client name
    /// </summary>
    public string ClientName { get; set; }

    /// <summary>
    /// Consuming messag unique id
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Retry count
    /// </summary>
    public int TryCount { get; set; }

    /// <summary>
    /// Create new description object from exception
    /// </summary>
    public static ExceptionDescription Create(HorseClient client, HorseMessage message, Exception exception, int tryCount)
    {
        return new ExceptionDescription
        {
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            MachineName = Environment.MachineName,
            TargetName = message.Target,
            ClientType = client.GetClientType(),
            ClientName = client.GetClientName(),
            TryCount = tryCount,
            MessageId = message.MessageId
        };
    }
}