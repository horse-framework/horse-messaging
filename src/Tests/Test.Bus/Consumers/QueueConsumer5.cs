using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

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