using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

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

        public Task Consume(TwinoMessage message, Model1 model, TmqClient client)
        {
            Count++;
            return Task.CompletedTask;
            //throw new System.NotImplementedException();
        }
    }
}