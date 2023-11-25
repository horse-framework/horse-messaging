using System;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Horse channel exception
/// </summary>
public class HorseChannelException : Exception
{
    /// <summary>
    /// Creates new horse channel exception
    /// </summary>
    public HorseChannelException(string message) : base(message)
    {
    }
}