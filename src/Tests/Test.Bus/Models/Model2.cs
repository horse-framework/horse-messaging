using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace Test.Bus.Models
{
    [PutBackDelay(4000)]
    [QueueName("model-2")]
    [QueueStatus(MessagingQueueStatus.Push)]
    [Acknowledge(QueueAckDecision.JustRequest)]
    public class Model2
    {
        public string Foo { get; set; }
    }
}