using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    public class ExceptionConsumer1 : IQueueConsumer<ExceptionModel1>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer1 Instance { get; private set; }

        public ExceptionConsumer1()
        {
            Instance = this;
        }

        public Task Consume(TwinoMessage message, ExceptionModel1 model, TmqClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}