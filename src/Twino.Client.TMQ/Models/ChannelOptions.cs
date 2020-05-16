using System.Text.Json.Serialization;

namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Channel creation options
    /// </summary>
    public class ChannelOptions : QueueOptions
    {
        /// <summary>
        /// If true, channel supports multiple queues
        /// </summary>
        [JsonPropertyName("AllowMultipleQueues")]
        public bool? AllowMultipleQueues { get; set; }

        /// <summary>
        /// Allowed queues for channel
        /// </summary>
        [JsonPropertyName("AllowedQueues")]
        public ushort[] AllowedQueues { get; set; }

        /// <summary>
        /// Registry key for channel event handler
        /// </summary>
        [JsonPropertyName("ChannelEventHandler")]
        public string EventHandler { get; set; }

        /// <summary>
        /// Registry key for channel authenticator
        /// </summary>
        [JsonPropertyName("Authenticator")]
        public string Authenticator { get; set; }

        /// <summary>
        /// Maximum client limit of the channel.
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int? ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the channel.
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("QueueLimit")]
        public int? QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        [JsonPropertyName("DestroyWhenEmpty")]
        public bool? DestroyWhenEmpty { get; set; }
    }
}