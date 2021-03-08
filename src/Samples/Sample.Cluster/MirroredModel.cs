using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;

namespace Sample.Cluster
{
    [QueueName("Mirror")]
    [QueueStatus(MessagingQueueStatus.Push)]
    public class MirroredModel
    {
        public string Foo { get; set; }
    }
}