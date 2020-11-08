using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    public class ExceptionConsumer3 : IQueueConsumer<ExceptionModel3>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer3 Instance { get; private set; }

        public ExceptionConsumer3()
        {
            Instance = this;
        }

        public Task Consume(TwinoMessage message, ExceptionModel3 model, TmqClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}