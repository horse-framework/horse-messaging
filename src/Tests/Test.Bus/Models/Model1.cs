using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

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