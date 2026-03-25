using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace ClusteringSample.Consumer;

[AutoAck]
public class FooConsumer : IQueueConsumer<Foo>
{
    public async Task Consume(ConsumeContext<Foo> context)
    {
        await Task.Delay(new Random().Next(50, 350), context.CancellationToken);
        Console.WriteLine($"Message #{context.Model.No} is Consumed");
    }
}
