using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Protocol;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Consumers
{
    [AutoAck]
    public class QueueConsumer1 : IQueueConsumer<Model1>
    {
        public int Count { get; private set; }

        public static QueueConsumer1 Instance { get; private set; }

        public QueueConsumer1()
        {
            Instance = this;
        }

        public Task Consume(HorseMessage message, Model1 model, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
            //throw new System.NotImplementedException();
        }
    }
}