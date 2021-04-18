using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [PutBackDelay(4000)]
    [QueueName("model-2")]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    public class Model2
    {
        public string Foo { get; set; }
    }
}