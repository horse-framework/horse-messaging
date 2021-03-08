using System;
using System.Threading.Tasks;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;

namespace Sample.Cluster
{
    public class MirroredModelConsumer : IQueueConsumer<MirroredModel>
    {
        public Task Consume(HorseMessage message, MirroredModel model, HorseClient client)
        {
            Console.WriteLine($"Consumed {model.Foo}");
            return Task.CompletedTask;
        }
    }
}