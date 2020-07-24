using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    public class DirectConsumerC : IDirectConsumer<ModelC>
    {
        public Task Consume(TmqMessage message, ModelC model, TmqClient client)
        {
            Console.WriteLine("Model C consumed");
            return Task.CompletedTask;
        }
    }
}