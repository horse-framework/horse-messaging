using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

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