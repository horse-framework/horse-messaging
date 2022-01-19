using System.Text.Json.Serialization;
using System.Threading;
using Newtonsoft.Json;

namespace Horse.Messaging.Protocol.Models
{
    /// <summary>
    /// Channel information
    /// </summary>
    public class ChannelInformation
    {
        /// <summary>
        /// Channel name
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Channel topic
        /// </summary>
        [JsonProperty("Topic")]
        [JsonPropertyName("Topic")]
        public string Topic { get; set; }

        /// <summary>
        /// Channel Status
        /// </summary>
        [JsonProperty("Status")]
        [JsonPropertyName("Status")]
        public string Status { get; set; }

        /// <summary>
        /// Total published message count by publishers
        /// </summary>
        [JsonProperty("Published")]
        [JsonPropertyName("Published")]
        public long Published { get; set; }
        
        /// <summary>
        /// Total receive count by subscribers.
        /// (Published Message Count x Subscriber Count) 
        /// An example, if there are 5 consumers in channel,
        /// When publisher publishes 10 messages, received value will be 50.
        /// </summary>
        [JsonProperty("Received")]
        [JsonPropertyName("Received")]
        public long Received { get; set; }

        /// <summary>
        /// Active subscriber count of the channel
        /// </summary>
        [JsonProperty("SubscriberCount")]
        [JsonPropertyName("SubscriberCount")]
        public int SubscriberCount { get; set; }
    }
}