using System;
using Horse.Mq.Client;
using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace RoutingSample.DirectConsumer
{
	internal class Program
	{
		public static IHorseRouteBus RouteBus;

		private static void Main(string[] args)
		{
			HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				HorseClient client = new HorseClient();
				client.SetClientType("SAMPLE-MESSAGE-CONSUMER");
				return client;
			});

			connector.AddHost("hmq://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Observer.RegisterConsumer<SampleDirectMessageConsumer>();
			connector.Connected += (c) => { Console.WriteLine("CONNECTED"); };
			connector.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
			connector.MessageReceived += (client, message) => Console.WriteLine("Direct message received");
			connector.Run();

			RouteBus = connector.Bus.Route;
			
			while (true)
				Console.ReadLine();
		}
	}
}