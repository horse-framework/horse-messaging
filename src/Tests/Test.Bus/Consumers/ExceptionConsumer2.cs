using System.Threading.Tasks;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [QueueName("ex-queue-2")]
    public class ExceptionConsumer2 : IQueueConsumer<string>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer2 Instance { get; private set; }

        public ExceptionConsumer2()
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