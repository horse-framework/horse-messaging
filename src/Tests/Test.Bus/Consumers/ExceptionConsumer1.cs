using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Test.Bus.Consumers
{
    [QueueName("ex-queue-1")]
    public class ExceptionConsumer1 : IQueueConsumer<string>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer1 Instance { get; private set; }

        public ExceptionConsumer1()
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