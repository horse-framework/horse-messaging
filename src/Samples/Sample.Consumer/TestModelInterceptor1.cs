using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Sample.Producer
{
	public class TestModelInterceptor1 : IHorseInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("MODEL INTERCEPTOR");
			return Task.CompletedTask;
		}
	}
}