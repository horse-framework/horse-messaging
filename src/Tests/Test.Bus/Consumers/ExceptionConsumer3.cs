using System.Threading.Tasks;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [QueueName("ex-queue-3")]
    public class ExceptionConsumer3 : IQueueConsumer<string>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer3 Instance { get; private set; }

        public ExceptionConsumer3()
        {
            Instance = this;
        }

        public Task Consume(TwinoMessage message, string model, TmqClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}