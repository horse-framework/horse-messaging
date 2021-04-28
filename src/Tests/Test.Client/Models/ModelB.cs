using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;

namespace Test.Client.Models
{
    [QueueName("model-b")]
    [QueueTopic("queue-topic")]
    [DelayBetweenMessages(50)]
    [PutBackDelay(2500)]
    [QueueType(MessagingQueueType.Push)]
    public class ModelB
    {
        public string Foo { get; set; }
    }
}