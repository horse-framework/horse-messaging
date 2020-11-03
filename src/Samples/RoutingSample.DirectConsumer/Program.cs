using System;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Client.TMQ.Connectors;

namespace RoutingSample.DirectConsumer
{
	internal class Program
	{
		public static ITwinoRouteBus RouteBus;

		private static void Main(string[] args)
		{
			TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				TmqClient client = new TmqClient();
				client.SetClientType("SAMPLE-MESSAGE-CONSUMER");
				return client;
			});

			connector.AddHost("tmq://localhost:15500");
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