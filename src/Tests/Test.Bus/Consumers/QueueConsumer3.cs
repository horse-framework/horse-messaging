using System;
using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [PushExceptions("ex-queue-1")]
    [PushExceptions(typeof(NotSupportedException), "ex-queue-2")]
    [PushExceptions(typeof(InvalidOperationException), "ex-queue-3")]
    public class QueueConsumer3 : IQueueConsumer<Model3>
    {
        public int Count { get; private set; }

        public static QueueConsumer3 Instance { get; private set; }

        public QueueConsumer3()
        {
            Instance = this;
        }

        public async Task Consume(TwinoMessage message, Model3 model, TmqClient client)
        {
            Count++;
            throw new NotImplementedException();
        }
    }
}