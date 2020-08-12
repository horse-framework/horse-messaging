using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sample.Route.Models;
using Twino.Client.TMQ.Bus;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Producer
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddTwinoBus(tmq =>
			{
				tmq.AddHost("tmq://localhost:22201");
				tmq.SetClientId("producer");
				tmq.SetClientType("sample-producer");
				tmq.EnhanceConnection(c => c.ResponseTimeout = TimeSpan.FromSeconds(555));
				tmq.OnConnected(connector => Console.WriteLine("CONNECTED => sample-producer"));
			});

			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus();

			var bus = provider.GetService<ITwinoRouteBus>();

			while (true)
			{
				if (!bus.GetClient().IsConnected) continue;
				var request = new SampleARequest
				{
					Name = "A-REQUEST",
					Guid = Guid.NewGuid()
				};
				var result = await bus.Execute<SampleARequest, List<SampleResult>>(request);
				Thread.Sleep(1000);
			}
		}
	}
}