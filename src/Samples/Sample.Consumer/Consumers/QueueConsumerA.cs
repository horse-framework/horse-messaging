using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    [AutoAck]
    [AutoNack]
    public class QueueConsumerA : IQueueConsumer<ModelA>
    {
        public Task Consume(TmqMessage message, ModelA model)
        {
            Console.WriteLine("Model A Consumed");
            return Task.CompletedTask;
        }
    }
}