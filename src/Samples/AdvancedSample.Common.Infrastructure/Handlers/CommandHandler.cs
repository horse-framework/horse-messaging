using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Handlers
{
	[AutoResponse(AutoResponse.All)]
	[Retry(count: 5, delayBetweenRetries: 250)]
	public abstract class CommandHandler<T> : IDirectMessageHandler<T> where T : IServiceCommand
	{
		public Task Handle(HorseMessage message, T model, HorseClient client)
		{
			return Handle(model);
		}

		protected abstract Task Handle(T command);
	}
}