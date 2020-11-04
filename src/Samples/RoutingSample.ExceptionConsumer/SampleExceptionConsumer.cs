using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Twino.MQ.Client;
using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

namespace RoutingSample.ExceptionConsumer
{
	[QueueName("SAMPLE-EXCEPTION-QUEUE")]
	[QueueStatus(MessagingQueueStatus.Push)]
	[AutoAck]
	public class SampleExceptionConsumer : IQueueConsumer<string>
	{
		public Task Consume(TwinoMessage message, string serializedException, TmqClient client)
		{
			Exception exception = JsonConvert.DeserializeObject<Exception>(serializedException);
			Console.WriteLine(exception.Message);
			return Task.CompletedTask;
		}
	}
}