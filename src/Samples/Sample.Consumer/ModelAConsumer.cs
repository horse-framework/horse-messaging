using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Sample.Consumer
{
	[AutoAck]
	[Interceptor(typeof(TestBeforeInterceptor1), 4)]
	[Interceptor(typeof(TestAfterInterceptor1), 1, false)]
	[Interceptor(typeof(TestAfterInterceptor2), 2, false)]
	public class ModelAConsumer : ModelABaseConsumer
	{
		protected override Task Handle(ModelA model)
		{
			_ = Console.Out.WriteLineAsync("CONSUMED");
			return Task.CompletedTask;
		}

		public async Task Consume(HorseMessage message, ModelA model, HorseClient client)
		{
			Console.WriteLine($"Consumed: {model.Foo} ({model.No})");
			var result = await client.Direct.RequestJson<ResponseModel>(new RequestModel());
			Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");
		}
	}
	
	[Interceptor(typeof(TestBeforeInterceptor2), 2)]
	public abstract class ModelABaseConsumer : IQueueConsumer<ModelA>
	{
		protected abstract Task Handle(ModelA model);
		public async Task Consume(HorseMessage message, ModelA model, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("CONSUMED BASE");
			await Handle(model);
		}
	}

		
}