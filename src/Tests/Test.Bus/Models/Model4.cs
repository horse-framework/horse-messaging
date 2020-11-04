using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

namespace Test.Bus.Models
{
    [HighPriorityMessage]
    [MessageHeader("X*Model", "4")]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model4
    {
        public string Foo { get; set; }
    }
}