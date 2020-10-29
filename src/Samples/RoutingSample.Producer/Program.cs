using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace RoutingSample.Producer
{
	internal class Program
	{
		private static async Task Main(string[] args)
		{
			TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2));
			connector.AddHost("tmq://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Run();

			ITwinoQueueBus queueBus = connector.Bus.Queue;
			ITwinoRouteBus routeBus = connector.Bus.Route;

			while (true)
			{
				TwinoResult result = await routeBus.PublishJson(new SampleMessage());
				// TwinoResult result = await queueBus.PushJson(new SampleMessage());
				Console.WriteLine($"Push: {result.Code}");
				await Task.Delay(5000);
			}
		}
	}
}