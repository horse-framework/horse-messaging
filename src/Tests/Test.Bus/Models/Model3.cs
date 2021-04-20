using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [QueueTopic("model-3-topic")]
    [QueueType(MessagingQueueType.RoundRobin)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model3
    {
        public string Foo { get; set; }
    }
}