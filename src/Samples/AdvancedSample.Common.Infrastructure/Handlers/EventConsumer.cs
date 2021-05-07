using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Handlers
{
	[AutoAck]
	[AutoNack(NegativeReason.ExceptionMessage)]
	[Retry(count: 5, delayBetweenRetries: 250)]
	public abstract class EventConsumer<T> : IQueueConsumer<T> where T : IServiceEvent
	{
		public Task Consume(HorseMessage message, T model, HorseClient client)
		{
			return Consume(model);
		}

		protected abstract Task Consume(T @event);
	}
}