using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

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