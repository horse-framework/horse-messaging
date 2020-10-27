using System;
using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [PublishExceptions("ex-route-1")]
    [PublishExceptions(typeof(NotSupportedException), "ex-route-2")]
    [PublishExceptions(typeof(InvalidOperationException), "ex-route-3")]
    public class QueueConsumer4 : IQueueConsumer<Model4>
    {
        public int Count { get; private set; }

        public static QueueConsumer4 Instance { get; private set; }

        public QueueConsumer4()
        {
            Instance = this;
        }

        public async Task Consume(TwinoMessage message, Model4 model, TmqClient client)
        {
            Count++;
            throw new NotImplementedException();
        }
    }
}