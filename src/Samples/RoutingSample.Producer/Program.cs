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
			IHorseQueueBus queueBus = connector.Bus.Queue;

			int counter = 1;
			while (true)
			{
				SampleMessage request = new SampleMessage
				{
					Content = "Hello"
				};

				/*
				GiveMeGuidRequest request = new()
				{
					Foo = "Hello from sample direct message consumer"
				};*/
				// Dictionary<string, string> headers = new()
				// {
				// 	{ "RequestId", Guid.NewGuid().ToString() },
				// 	{ "UserId", "12" }
				// };
				// HorseResult<GiveMeGuidResponse> result = await routeBus.PublishRequestJson<SampleMessage, GiveMeGuidResponse>(request, headers);
				// if (result.Code == HorseResultCode.NotFound)
				// 	Debugger.Break();
				//
				// Console.WriteLine($"Push: {result.Code}");

				Dictionary<string, string> headers = new()
				{
					{ "Id", counter.ToString() },
				};

				await queueBus.PushJson(new SampleMessage
				{
					Content = "Lorem ipsum dolor sit amet"
				}, false, headers);
				counter++;
				Console.ReadLine();
			}
		}
	}
}