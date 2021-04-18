using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Protocol;
using Newtonsoft.Json;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

namespace RoutingSample.ExceptionConsumer
{
	[QueueName("SAMPLE-EXCEPTION-QUEUE")]
	[QueueStatus(MessagingQueueStatus.Push)]
	[AutoAck]
	public class SampleExceptionConsumer : IQueueConsumer<string>
	{
		public Task Consume(HorseMessage message, string serializedException, HorseClient client)
		{
			Exception exception = JsonConvert.DeserializeObject<Exception>(serializedException);
			Console.WriteLine(exception.Message);
			return Task.CompletedTask;
		}
	}
}