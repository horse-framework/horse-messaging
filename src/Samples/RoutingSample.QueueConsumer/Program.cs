using System;
using Horse.Messaging.Client;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace RoutingSample.QueueConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2));
			connector.AddHost("horse://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Observer.RegisterConsumer<SampleMessageQueueConsumer>();
			connector.Connected += (c) =>
			{
				Console.WriteLine("CONNECTED");
				_ = connector.GetClient().Queues.Subscribe("SAMPLE-MESSAGE-QUEUE", false);
			};
			connector.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
			connector.MessageReceived += (client, message) => Console.WriteLine("Queue message received");
			connector.Run();

			while (true)
				Console.ReadLine();
		}
	}
}