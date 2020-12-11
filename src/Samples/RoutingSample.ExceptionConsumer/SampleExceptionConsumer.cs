using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

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