using System;
using System.Threading.Tasks;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Protocols.Hmq;

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

        public async Task Consume(HorseMessage message, Model5 model, HorseClient client)
        {
            Count++;
            throw new NotImplementedException();
        }
    }
}