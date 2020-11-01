using System;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;

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
	}
}