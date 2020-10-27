using System.Threading.Tasks;
using Test.Bus.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

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

        public async Task Consume(TwinoMessage message, Model2 model, TmqClient client)
        {
            Count++;
            throw new System.NotImplementedException();
        }
    }
}