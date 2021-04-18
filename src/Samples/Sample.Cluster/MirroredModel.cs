using Horse.Messaging.Server.Client.Annotations;
using Horse.Messaging.Server.Client.Models;
using Horse.Mq.Client.Annotations;

namespace Sample.Cluster
{
    [QueueName("Mirror")]
    [QueueStatus(MessagingQueueStatus.Push)]
    public class MirroredModel
    {
        public string Foo { get; set; }
    }
}