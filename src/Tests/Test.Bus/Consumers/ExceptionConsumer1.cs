using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;

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

        public Task Consume(HorseMessage message, ExceptionModel1 model, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}