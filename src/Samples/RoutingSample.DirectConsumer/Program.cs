using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace RoutingSample.DirectConsumer
{
	internal class Program
	{
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
			connector.MessageReceived +=(client, message) =>  Console.WriteLine("mmmm");
			connector.Run();

			while (true)
				Console.ReadLine();
		}
	}

	public class SampleDirectMessageConsumer : IDirectConsumer<SampleMessage>
	{
		public Task Consume(TwinoMessage message, SampleMessage model, TmqClient client)
		{
			Console.WriteLine("SAMPLE DIRECT MESSAGE RECEIVED");
			return Task.CompletedTask;
		}
	}
}