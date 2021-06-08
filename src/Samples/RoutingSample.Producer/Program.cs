using System;
using System.Collections.Generic;
using System.Diagnostics;
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
				GiveMeGuidRequest request = new()
				{
					Foo = "Hello from sample direct message consumer"
				};
				Dictionary<string, string> headers = new()
				{
					{ "RequestId", Guid.NewGuid().ToString() },
					{ "UserId", "12" }
				};
				HorseResult<GiveMeGuidResponse> result = await routeBus.PublishRequestJson<GiveMeGuidRequest, GiveMeGuidResponse>(request, headers);
				if (result.Code == HorseResultCode.NotFound)
					Debugger.Break();
				Console.WriteLine($"Push: {result.Code}");
			}
		}
	}
}