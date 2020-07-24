using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    [PushExceptions("error", 500)]
    public class QueueConsumerB : IQueueConsumer<ModelB>
    {
        public Task Consume(TmqMessage message, ModelB model, TmqClient client)
        {
            Console.WriteLine("Model B consumed");
            Random rnd = new Random();
            if (rnd.NextDouble() < 0.5)
                throw new ApplicationException();
            
            return Task.CompletedTask;
        }
    }
}