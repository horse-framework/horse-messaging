using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Routers;

/// <summary>
/// Type descriptor for router messages
/// </summary>
public class RouterTypeDescriptor : ITypeDescriptor
{
    /// <summary>
    /// Router Name
    /// </summary>
    public string RouterName { get; set; }

    /// <summary>
    /// True, if router name specified
    /// </summary>
    public bool HasRouterName { get; set; }

    /// <summary>
    /// Content Type
    /// </summary>
    public ushort ContentType { get; set; }

    /// <summary>
    /// Message model type
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// If true, message is sent as high priority
    /// </summary>
    public bool HighPriority { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, queue topic.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Headers for delivery descriptor of type
    /// </summary>
    public List<KeyValuePair<string, string>> Headers { get; }

    /// <summary>
    /// Creates new type delivery descriptor
    /// </summary>
    public RouterTypeDescriptor()
    {
        Headers = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Applies descriptor information to the message
    /// </summary>
    public HorseMessage CreateMessage(string overwrittenTarget = null)
    {
        HorseMessage message = new HorseMessage(MessageType.Router, overwrittenTarget ?? RouterName, ContentType);
        if (HighPriority)
            message.HighPriority = HighPriority;

        if (!string.IsNullOrEmpty(Topic))
            message.AddHeader(HorseHeaders.QUEUE_TOPIC, Topic);

        foreach (KeyValuePair<string, string> pair in Headers)
            message.AddHeader(pair.Key, pair.Value);

        return message;
    }
}