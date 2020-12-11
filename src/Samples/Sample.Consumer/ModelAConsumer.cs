using System;
using System.Threading.Tasks;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Protocols.Hmq;

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