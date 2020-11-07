using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

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