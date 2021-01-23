using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

namespace Test.Bus.Models
{
    [QueueTopic("model-3-topic")]
    [QueueStatus(MessagingQueueStatus.RoundRobin)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model3
    {
        public string Foo { get; set; }
    }
}