using System;
using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

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

        public Task Consume(TwinoMessage message, Model4 model, TmqClient client)
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