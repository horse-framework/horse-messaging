using System;

namespace Horse.Messaging.Client.Routers.Annotations;

/// <summary>
/// Describes topic for router
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RouterTopicAttribute : Attribute
{
    /// <summary>
    /// Topic
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Creates new router topic attribute
    /// </summary>
    public RouterTopicAttribute(string topic)
    {
        Topic = topic;
    }
}