using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Horse.Messaging.Server.Client.Annotations;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

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