using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace RoutingSample.DirectConsumer
{
	[AutoAck]
	[AutoNack(NackReason.ExceptionType)]
	[PushExceptions("SAMPLE-EXCEPTION-QUEUE")]
	public abstract class BaseDirectConsumer<T> : IDirectConsumer<T>
	{
		protected abstract Task Handle(T model);

		public Task Consume(TwinoMessage message, T model, TmqClient client)
		{
			// Uncomment for test exception messages queue
			// throw new Exception("Something was wrong.");
			return Handle(model);
		}
	}
}