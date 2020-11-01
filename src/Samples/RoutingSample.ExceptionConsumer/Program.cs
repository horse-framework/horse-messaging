using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Connectors;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace RoutingSample.ExceptionConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				TmqClient client = new TmqClient();
				client.SetClientType("SAMPLE-EXCEPTION-CONSUMER");
				return client;
			});
			connector.AddHost("tmq://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Observer.RegisterConsumer<SampleExceptionConsumer>();
			connector.Connected += (c) =>
			{
				Console.WriteLine("CONNECTED");
				_ = connector.GetClient().Queues.Subscribe("SAMPLE-EXCEPTION-QUEUE", false);
			};
			connector.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
			connector.MessageReceived += (client, message) => Console.WriteLine("Direct message received");
			connector.Run();

			while (true)
				Console.ReadLine();
		}

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
}