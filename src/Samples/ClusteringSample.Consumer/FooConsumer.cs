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
        public async Task Consume(HorseMessage message, Foo model, HorseClient client)
        {
            await Task.Delay(new Random().Next(50, 250));
            Console.WriteLine($"Message #{model.No} is Consumed");
        }
    }
}