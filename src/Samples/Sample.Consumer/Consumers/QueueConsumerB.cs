using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    public class QueueConsumerB : IQueueConsumer<ModelB>
    {
        public Task Consume(TmqMessage message, ModelB model)
        {
            Console.WriteLine("Model B consumed");
            return Task.CompletedTask;
        }
    }
}