using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;

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