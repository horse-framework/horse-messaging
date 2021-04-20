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
    [PublishExceptions(typeof(ExceptionModel1))]
    [PublishExceptions(typeof(ExceptionModel2), typeof(NotSupportedException))]
    [PublishExceptions(typeof(ExceptionModel3), typeof(InvalidOperationException))]
    public class QueueConsumer4 : IQueueConsumer<Model4>
    {
        public int Count { get; private set; }

        public static QueueConsumer4 Instance { get; private set; }

        public QueueConsumer4()
        {
            Instance = this;
        }

        public Task Consume(HorseMessage message, Model4 model, HorseClient client)
        {
            Count++;

            if (Count == 2)
                throw new NotSupportedException();

            if (Count == 3)
                throw new InvalidOperationException();

            throw new InvalidCastException();
        }
    }
}