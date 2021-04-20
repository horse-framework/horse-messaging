using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Sample.Consumer
{
    [AutoAck]
    public class ModelAConsumer : IQueueConsumer<ModelA>
    {
        public Task Consume(HorseMessage message, ModelA model, HorseClient client)
        {
            Console.WriteLine($"Consumed: {model.Foo} ({model.No})");
            return Task.CompletedTask;
        }
    }
}