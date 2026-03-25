using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;

namespace Sample.Cluster;

public class MirroredModelConsumer : IQueueConsumer<MirroredModel>
{
    public Task Consume(ConsumeContext<MirroredModel> context)
    {
        Console.WriteLine($"Consumed {context.Model.Foo}");
        return Task.CompletedTask;
    }
}
