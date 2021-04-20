using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [DelayBetweenMessages(300)]
    [QueueType(MessagingQueueType.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    public class Model1
    {
        public string Foo { get; set; }
    }
}