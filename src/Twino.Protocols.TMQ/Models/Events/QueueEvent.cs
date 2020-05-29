using System.Text.Json.Serialization;

namespace Twino.Protocols.TMQ.Models.Events
{
    /// <summary>
    /// Queue event info model
    /// </summary>
    public class QueueEvent
    {
        /// <summary>
        /// Channel name
        /// </summary>
        [JsonPropertyName("Channel")]
        public string Channel { get; set; }

        /// <summary>
        /// Queue Id
        /// </summary>
        [JsonPropertyName("Id")]
        public ushort Id { get; set; }

        /// <summary>
        /// Queue Tag
        /// </summary>
        [JsonPropertyName("Tag")]
        public string Tag { get; set; }

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
}