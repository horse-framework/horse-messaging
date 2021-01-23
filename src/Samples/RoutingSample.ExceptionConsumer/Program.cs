using System;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace RoutingSample.ExceptionConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				HorseClient client = new HorseClient();
				client.SetClientType("SAMPLE-EXCEPTION-CONSUMER");
				return client;
			});
			connector.AddHost("hmq://localhost:15500");
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
	}
}