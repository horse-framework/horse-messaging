using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

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

	[AutoAck]
	[AutoNack]
	public class SampleMessageQueueConsumer : IQueueConsumer<SampleMessage>
	{
		public Task Consume(TwinoMessage message, SampleMessage model, TmqClient client)
		{
			Console.WriteLine("SAMPLE QUEUE MESSAGE CONSUMED");
			return Task.CompletedTask;
		}
	}
}