using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Test.Client.Models;

namespace Test.Client.Handlers;

[AutoAck]
[AutoNack]
public class QueueConsumer : IQueueConsumer<ModelB>
{
    public Task Consume(HorseMessage message, ModelB model, HorseClient client)
    {
        return Task.CompletedTask;
    }
}