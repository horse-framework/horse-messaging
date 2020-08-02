using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    [AutoAck]
    [PushExceptions(typeof(NotSupportedException), "error", 500)]
    [PushExceptions(typeof(ApplicationException), "error", 501)]
    [PushExceptions("error", 502)]
    public class QueueConsumerB : IQueueConsumer<ModelB>
    {
        public Task Consume(TmqMessage message, ModelB model, TmqClient client)
        {
            Console.WriteLine("Model B consumed");
            Random rnd = new Random();
            /*
            if (rnd.NextDouble() < 0.1)
                throw new NotSupportedException();
            
            if (rnd.NextDouble() < 0.5)
                throw new ApplicationException();
            */
            return Task.CompletedTask;
        }
    }
}