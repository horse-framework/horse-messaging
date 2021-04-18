using System.Text.Json.Serialization;
using Horse.Messaging.Server.Client.Annotations;
using Horse.Messaging.Server.Client.Models;
using Horse.Messaging.Server.Client.Queues.Annotations;
using Newtonsoft.Json;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client.Annotations;

namespace Sample.Producer
{
 //   [QueueName("model-a")]
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