using System;

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
    /// Create new description object from exception
    /// </summary
    public static ExceptionDescription Create(Exception exception)
    {
        return new ExceptionDescription
        {
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            MachineName = Environment.MachineName
        };
    }
}