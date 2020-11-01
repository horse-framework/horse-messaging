using System;
using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [AutoAck]
    [AutoNack]
    [Retry(5, 50, typeof(InvalidCastException))]
    public class QueueConsumer5 : IQueueConsumer<Model5>
    {
        public int Count { get; private set; }

        public static QueueConsumer5 Instance { get; private set; }

        public QueueConsumer5()
        {
            Instance = this;
        }

        public async Task Consume(TwinoMessage message, Model5 model, TmqClient client)
        {
            Count++;
            throw new NotImplementedException();
        }
    }
}