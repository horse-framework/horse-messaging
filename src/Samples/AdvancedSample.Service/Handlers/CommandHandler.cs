using System;
using System.Threading.Tasks;
using AdvancedSample.Service.Interceptors;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Service.Handlers
{
	[AutoResponse(AutoResponse.OnSuccess)]
	[Interceptor(typeof(TestInterceptor))]
	public abstract class CommandHandler<T> : IDirectMessageHandler<T>
	{
		protected abstract Task Execute(T command);

		public async Task Handle(HorseMessage message, T model, HorseClient client)
		{
			await Task.Delay(15000);
			await Execute(model);
		}

		private static async Task OnError(HorseMessage message, HorseClient client, Exception exception)
		{
			await client.SendNegativeAck(message, exception.Message);
		}
	}
}