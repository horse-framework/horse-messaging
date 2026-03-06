using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace RoutingSample.DirectConsumer
{
    [AutoAck]
    [AutoNack(NegativeReason.ExceptionType)]
    public abstract class BaseDirectMessageHandler<T> : IDirectMessageHandler<T>
    {
        protected abstract Task Handle(T model);

        public Task Handle(HorseMessage message, T model, HorseClient client,
            CancellationToken cancellationToken = default)
        {
            // Uncomment for test exception messages queue
            // throw new Exception("Something was wrong.");
            return Handle(model);
        }
    }
}