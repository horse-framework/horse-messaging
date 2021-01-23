using System.Text.Json.Serialization;

namespace Horse.Protocols.Hmq.Models.Events
{
    /// <summary>
    /// Queue message event info model
    /// </summary>
    public class MessageEvent
    {
        /// <summary>
        /// Queue name
        /// </summary>
        [JsonPropertyName("Queue")]
        public string Queue { get; set; }

        /// <summary>
        /// Message Unique Id
        /// </summary>
        [JsonPropertyName("Id")]
        public string Id { get; set; }

        /// <summary>
        /// True, if message is saved persistent
        /// </summary>
        [JsonPropertyName("Saved")]
        public bool Saved { get; set; }

        /// <summary>
        /// Message Producer Client Id
        /// </summary>
        [JsonPropertyName("ProducerId")]
        public string ProducerId { get; set; }

        /// <summary>
        /// Message Producer Client Name
        /// </summary>
        [JsonPropertyName("ProducerName")]
        public string ProducerName { get; set; }

        /// <summary>
        /// Message Producer Client Type
        /// </summary>
        [JsonPropertyName("ProducerType")]
        public string ProducerType { get; set; }
    }
}