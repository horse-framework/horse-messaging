using System.Threading.Tasks;
using AdvancedSample.Service.Interceptors;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Service.Handlers;

[AutoAck]
[Interceptor(typeof(TestInterceptor))]
public abstract class EventHandler<T> : IQueueConsumer<T>
{
    protected abstract Task Execute(T command);

    public async Task Consume(HorseMessage message, T model, HorseClient client)
    {
        await Execute(model);
    }
}