using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;

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