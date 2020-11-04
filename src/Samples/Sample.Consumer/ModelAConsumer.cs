using System;
using System.Threading.Tasks;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer
{
    [AutoAck]
    public class ModelAConsumer : IQueueConsumer<ModelA>
    {
        public Task Consume(TwinoMessage message, ModelA model, TmqClient client)
        {
            Console.WriteLine($"Consumed: {model.Foo} ({model.No})");
            return Task.CompletedTask;
        }
    }
}