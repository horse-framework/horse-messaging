using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;
using Newtonsoft.Json;

namespace HostedServiceSample.Producer
{
	[DirectContentType(1)]
	public class TestDirectModelHandler: IDirectMessageHandler<TestDirectModel>
	{
		public Task Handle(HorseMessage message, TestDirectModel model, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("Consumed!!!");
			_ = Console.Out.WriteLineAsync(JsonConvert.SerializeObject(model, Formatting.Indented));
			return Task.CompletedTask;
		}
	}
}