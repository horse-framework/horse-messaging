using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

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