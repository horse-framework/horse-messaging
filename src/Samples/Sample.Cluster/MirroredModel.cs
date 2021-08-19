using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;

namespace Sample.Cluster
{
    [QueueName("Mirror")]
    [QueueType(MessagingQueueType.Push)]
    public class MirroredModel
    {
        public string Foo { get; set; }
    }
}