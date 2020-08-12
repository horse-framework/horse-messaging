using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Consume2
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddTwinoBus(tmq =>
			{
				tmq.AddHost("tmq://localhost:22201");
				tmq.SetClientType("sample-b-consumer");
				tmq.AddTransientConsumers(typeof(Program));
				tmq.EnhanceConnection(c => c.ResponseTimeout = TimeSpan.FromSeconds(555));
				tmq.OnConnected(connector => Console.WriteLine($"CONNECTED => sample-b-consumer"));
			});

			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus();

			while (true) Thread.Sleep(250);
		}
	}
}