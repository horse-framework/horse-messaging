using System;

namespace Horse.Messaging.Client.Queues.Exceptions;

/// <summary>
/// Throw when reigstered same type of Exception with PushExceptions attribute
/// </summary>
public class DuplicatePushException : Exception
{
    /// <summary>
    /// Creates new DuplicatePushException
    /// </summary>
    public DuplicatePushException(string message) : base(message)
    {
    }
}