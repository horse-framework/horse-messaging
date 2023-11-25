using System.Text.Json.Serialization;

namespace Horse.Messaging.Protocol.Models.Events;

/// <summary>
/// Queue event info model
/// </summary>
public class QueueEvent
{
    /// <summary>
    /// Queue name
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    /// <summary>
    /// Queue Topic
    /// </summary>
    [JsonPropertyName("Topic")]
    public string Topic { get; set; }

    /// <summary>
    /// Queue current status
    /// </summary>
    [JsonPropertyName("Status")]
    public string Status { get; set; }

    /// <summary>
    /// Pending high priority messages in the queue
    /// </summary>
    [JsonPropertyName("PriorityMessages")]
    public int PriorityMessages { get; set; }

    /// <summary>
    /// Pending regular messages in the queue
    /// </summary>
    [JsonPropertyName("Messages")]
    public int Messages { get; set; }

    /// <summary>
    /// If event is raised in different node instance, the name of the instance.
    /// If null, event is raised in same instance.
    /// </summary>
    [JsonPropertyName("Node")]
    public string Node { get; set; }
}