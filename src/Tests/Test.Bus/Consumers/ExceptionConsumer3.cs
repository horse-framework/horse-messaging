using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;

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

        public Task Consume(HorseMessage message, ExceptionModel3 model, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}