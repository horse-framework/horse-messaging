using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [DelayBetweenMessages(300)]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    public class Model1
    {
        public string Foo { get; set; }
    }
}