using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace Sample.Producer
{
    [QueueName("model-a")]
    [DeliveryHandler("dhand")]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    public class ModelA
    {
        [JsonProperty("no")]
        [JsonPropertyName("no")]
        public int No { get; set; }

        [JsonProperty("foo")]
        [JsonPropertyName("foo")]
        public string Foo { get; set; }
    }
}