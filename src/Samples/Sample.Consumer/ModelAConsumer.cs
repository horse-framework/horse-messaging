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
	[AutoNack]
	[QueueName("SampleTestEvent")]
	public class ModelAConsumer : IQueueConsumer<ModelA>
	{
		public Task Consume(HorseMessage message, ModelA model, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("CONSUMED");
			return Task.CompletedTask;
		}
	}
		
}