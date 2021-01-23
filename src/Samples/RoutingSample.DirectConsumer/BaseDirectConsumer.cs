using System.Threading.Tasks;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Protocols.Hmq;

namespace RoutingSample.DirectConsumer
{
	[AutoAck]
	[AutoNack(NackReason.ExceptionType)]
	//[PushExceptions("SAMPLE-EXCEPTION-QUEUE")]
	public abstract class BaseDirectConsumer<T> : IDirectConsumer<T>
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