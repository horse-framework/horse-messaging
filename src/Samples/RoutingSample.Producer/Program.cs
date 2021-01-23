using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace RoutingSample.Producer
{
	internal class Program
	{
		private static async Task Main(string[] args)
		{
			HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2));
			connector.AddHost("hmq://localhost:15500");
			connector.ContentSerializer = new NewtonsoftContentSerializer();
			connector.Run();

			IHorseRouteBus routeBus = connector.Bus.Route;

			while (true)
			{
				HorseResult result = await routeBus.PublishJson(new SampleMessage(), true);
				Console.WriteLine($"Push: {result.Code}");
				await Task.Delay(5000);
			}
		}
	}
}