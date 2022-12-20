using System.Text.Json.Serialization;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Newtonsoft.Json;
using Sample.Producer;

namespace Sample.Consumer
{
    [QueueName("model-g")]
    //[QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    [Interceptor(typeof(TestModelInterceptor1))]
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