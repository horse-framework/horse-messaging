using System;
using Horse.Messaging.Client.Queues.Annotations;

namespace Horse.Messaging.Client.Direct.Annotations;

/// <summary>
/// Auto response for direct message handlers
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AutoResponseAttribute : Attribute
{
    /// <summary>
    /// Decides when message will be respond
    /// </summary>
    public AutoResponse Response { get; }
        
    /// <summary>
    /// Error type for failed operations
    /// </summary>
    public NegativeReason Error { get; }

    /// <summary>
    /// Creates new auto response attribute
    /// </summary>
    public AutoResponseAttribute(AutoResponse response, NegativeReason error = NegativeReason.None)
    {
        Response = response;
        Error = error;
    }
}