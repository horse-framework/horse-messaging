using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;

namespace Test.Bus.Models
{
    [QueueTopic("model-3-topic")]
    [QueueStatus(MessagingQueueStatus.RoundRobin)]
    public class Model3
    {
        public string Foo { get; set; }
    }
}