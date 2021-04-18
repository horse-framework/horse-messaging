using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Consumers
{
    [AutoAck]
    [AutoNack]
    [PushExceptions(typeof(ExceptionModel1))]
    [PushExceptions(typeof(ExceptionModel2), typeof(NotSupportedException))]
    [PushExceptions(typeof(ExceptionModel3), typeof(InvalidOperationException))]
    public class QueueConsumer3 : IQueueConsumer<Model3>
    {
        public int Count { get; private set; }

        public static QueueConsumer3 Instance { get; private set; }

        public QueueConsumer3()
        {
            Instance = this;
        }

        public Task Consume(HorseMessage message, Model3 model, HorseClient client)
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