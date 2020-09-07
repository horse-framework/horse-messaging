using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Consumer1
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddTwinoBus(tmq =>
			{
				tmq.AddHost("tmq://localhost:22201");
				tmq.SetClientType("sample-a-consumer");
				tmq.SetClientId("consumer1");
				tmq.AddScopedConsumers(typeof(Program));
				tmq.EnhanceConnection(c => c.ResponseTimeout = TimeSpan.FromSeconds(5));
				tmq.OnConnected(connector => Console.WriteLine($"CONNECTED => sample-a-consumer"));
			});

			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus();

			while (true) Thread.Sleep(250);
		}
	}
}