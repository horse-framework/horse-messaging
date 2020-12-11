using System.Threading.Tasks;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Protocols.Hmq;

namespace Test.Bus.Consumers
{
    [AutoNack(NackReason.ExceptionMessage)]
    public class QueueConsumer2  : IQueueConsumer<Model2>
    {
        public int Count { get; private set; }

        public static QueueConsumer2 Instance { get; private set; }

        public QueueConsumer2()
        {
            Instance = this;
        }

        public async Task Consume(HorseMessage message, Model2 model, HorseClient client)
        {
            Count++;
            throw new System.NotImplementedException();
        }
    }
}