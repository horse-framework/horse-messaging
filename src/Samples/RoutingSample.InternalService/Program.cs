using System;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;

namespace RoutingSample.InternalService
{
	class Program
	{
		static void Main(string[] args)
		{
			TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2), () =>
			{
				TmqClient client = new TmqClient();
				client.SetClientName("GIVE-ME-GUID-REQUEST-HANDLER-CONSUMER");
				return client;
			});


			connector.AddHost("tmq://localhost:15500");
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