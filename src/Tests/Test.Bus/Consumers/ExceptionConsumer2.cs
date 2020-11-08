using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    public class ExceptionConsumer2 : IQueueConsumer<ExceptionModel2>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer2 Instance { get; private set; }

        public ExceptionConsumer2()
        {
            Instance = this;
        }

        public Task Consume(TwinoMessage message, ExceptionModel2 model, TmqClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}