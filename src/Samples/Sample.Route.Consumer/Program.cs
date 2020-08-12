using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Twino.Client.TMQ;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Consumer
{
	class Program
	{
		static void Main(string[] args)
		{
			var serviceA = new ServiceCollection();
			var serviceB = new ServiceCollection();

			BuildConsumer(serviceA, "sample-a-consumer", cfg => { cfg.AddTransientConsumers(typeof(Program)); });
			BuildConsumer(serviceB, "sample-b-consumer", cfg => { cfg.AddTransientConsumers(typeof(Program)); });

			RunConsumer(serviceA);
			RunConsumer(serviceB);

			while (true)
				Thread.Sleep(250);
		}

		private static void BuildConsumer(IServiceCollection services, string clientType, Action<TwinoConnectorBuilder> configure)
		{
			services.AddTwinoBus(tmq =>
			{
				tmq.AddHost("tmq://localhost:22201");
				tmq.SetClientType(clientType);
				tmq.OnConnected(connector => Console.WriteLine($"CONNECTED => {clientType}"));
				configure(tmq);
			});
		}

		private static IServiceProvider RunConsumer(IServiceCollection services)
		{
			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus();
			return provider;
		}
	}
}