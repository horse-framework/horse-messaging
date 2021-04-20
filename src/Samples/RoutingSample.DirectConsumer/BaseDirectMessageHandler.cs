using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

namespace RoutingSample.DirectConsumer
{
	[AutoAck]
	[AutoNack(NackReason.ExceptionType)]
	//[PushExceptions("SAMPLE-EXCEPTION-QUEUE")]
	public abstract class BaseDirectMessageHandler<T> : IDirectMessageHandler<T>
	{
		protected abstract Task Handle(T model);

		public Task Consume(HorseMessage message, T model, HorseClient client)
		{
			// Uncomment for test exception messages queue
			// throw new Exception("Something was wrong.");
			return Handle(model);
		}
	}
}