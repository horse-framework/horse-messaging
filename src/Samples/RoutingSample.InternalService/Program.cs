using System;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace RoutingSample.InternalService
{
	class Program
	{
		static void Main(string[] args)
		{
			HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				HorseClient client = new HorseClient();
				client.SetClientName("GIVE-ME-GUID-REQUEST-HANDLER-CONSUMER");
				return client;
			});


			connector.AddHost("hmq://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Observer.RegisterConsumer<GiveMeGuidRequestHandler>();
			connector.Connected += (c) => { Console.WriteLine("CONNECTED"); };
			connector.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
			connector.MessageReceived += (client, message) => Console.WriteLine("Direct message received");
			connector.Run();

			while (true)
				Console.ReadLine();
		}
	}
}