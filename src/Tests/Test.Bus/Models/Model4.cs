using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [HighPriorityMessage]
    [MessageHeader("X-Model", "4")]
    [QueueType(MessagingQueueType.Push)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model4
    {
        public string Foo { get; set; }
    }
}