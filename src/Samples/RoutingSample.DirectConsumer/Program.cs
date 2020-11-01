using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
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
			connector.MessageReceived += (client, message) => Console.WriteLine("Direct message received");
			connector.Run();

			while (true)
				Console.ReadLine();
		}
	}

	public class SampleDirectMessageConsumer : BaseDirectConsumer<SampleMessage>
	{
		protected override Task Handle(SampleMessage model)
		{
			Console.WriteLine("SAMPLE DIRECT MESSAGE CONSUMED");
			return Task.CompletedTask;
		}
	}

	[AutoAck]
	[AutoNack(NackReason.ExceptionType)]
	[PushExceptions("SAMPLE-EXCEPTION-QUEUE")]
	public abstract class BaseDirectConsumer<T> : IDirectConsumer<T>
	{
		protected abstract Task Handle(T model);

		public Task Consume(TwinoMessage message, T model, TmqClient client)
		{
			// Uncomment for test exception messages queue
			// throw new Exception("Something was wrong.");
			return Handle(model);
		}
	}
}