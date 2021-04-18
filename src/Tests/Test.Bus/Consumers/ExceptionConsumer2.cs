using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;

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

        public Task Consume(HorseMessage message, ExceptionModel2 model, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}