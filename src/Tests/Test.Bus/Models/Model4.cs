using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

namespace Test.Bus.Models
{
    [HighPriorityMessage]
    [MessageHeader("X-Model", "4")]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model4
    {
        public string Foo { get; set; }
    }
}