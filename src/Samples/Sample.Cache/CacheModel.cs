using Horse.Messaging.Server.Client.Annotations;
using Horse.Messaging.Server.Client.Models;
using Horse.Messaging.Server.Client.Queues.Annotations;
using Horse.Mq.Client.Annotations;

namespace Sample.Cache
{
    [QueueName("KeyA")]
    [MessageTimeout(10)]
    [QueueStatus(MessagingQueueStatus.Cache)]
    public class CacheModel
    {
        public string Foo { get; set; }
    }
}