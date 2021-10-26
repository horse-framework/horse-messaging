using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace ClusteringSample.Consumer
{
    [AutoAck]
    public class FooConsumer : IQueueConsumer<Foo>
    {
        public Task Consume(HorseMessage message, Foo model, HorseClient client)
        {
            Console.WriteLine($"Message #{model.No} is Consumed");
            return Task.CompletedTask;
        }
    }
}