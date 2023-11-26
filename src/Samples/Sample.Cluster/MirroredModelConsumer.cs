using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;

namespace Sample.Cluster;

public class MirroredModelConsumer : IQueueConsumer<MirroredModel>
{
    public Task Consume(HorseMessage message, MirroredModel model, HorseClient client)
    {
        Console.WriteLine($"Consumed {model.Foo}");
        return Task.CompletedTask;
    }
}