using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace Test.Bus.Models
{
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.WaitForAcknowledge)]
    public class Model5
    {
        public string Foo { get; set; }
    }
}