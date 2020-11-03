using System;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;

namespace RoutingSample.QueueConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2));
			connector.AddHost("tmq://localhost:15500");
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