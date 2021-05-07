using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Sample.Consumer
{
	[AutoAck]
	public class ModelAConsumer : IQueueConsumer<ModelA>
	{
		public async Task Consume(HorseMessage message, ModelA model, HorseClient client)
		{
			Console.WriteLine($"Consumed: {model.Foo} ({model.No})");
			var result = await client.Direct.RequestJson<ResponseModel>(new RequestModel());
			Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");

		}
	}
}