using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Handlers
{
	[AutoResponse(AutoResponse.All)]
	public abstract class CommandHandler<T> : IDirectMessageHandler<T> where T : IServiceCommand
	{
		public Task Consume(HorseMessage message, T model, HorseClient client)
		{
			return Handle(model);
		}

		protected abstract Task Handle(T command);
	}
}