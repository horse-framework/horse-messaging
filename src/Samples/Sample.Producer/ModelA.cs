using System.Text.Json.Serialization;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Sample.Producer;

[QueueName("model-g")]
[QueueType(MessagingQueueType.Pull)]
[Acknowledge(QueueAckDecision.JustRequest)]
[Interceptor(typeof(TestModelInterceptor1))]
public class ModelA
{
    [JsonPropertyName("no")]
    public int No { get; set; }

    [JsonPropertyName("foo")]
    public string Foo { get; set; }
}

public class ModelC
{
    [JsonPropertyName("no")]
    public int No { get; set; }
}