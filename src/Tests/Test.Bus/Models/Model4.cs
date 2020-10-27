using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;

namespace Test.Bus.Models
{
    [HighPriorityMessage]
    [MessageHeader("X*Model", "4")]
    [QueueStatus(MessagingQueueStatus.Push)]
    public class Model4
    {
        public string Foo { get; set; }
    }
}