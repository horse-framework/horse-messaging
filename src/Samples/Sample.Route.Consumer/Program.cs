using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Twino.Client.TMQ;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Consumer
{
	public interface IConsumerA { }

	interface IConsumerB { }

	class Program
	{
		static void Main(string[] args)
		{
			var serviceA = new ServiceCollection();
			var serviceB = new ServiceCollection();

			BuildConsumer<IConsumerA>(serviceA, "sample-a-consumer", cfg => { cfg.AddTransientConsumer<SampleARequestHandler>(); });
			BuildConsumer<IConsumerB>(serviceB, "sample-b-consumer", cfg => { cfg.AddTransientConsumer<SampleBRequestHandler>(); });

			RunConsumer<IConsumerA>(serviceA);
			RunConsumer<IConsumerB>(serviceB);

			while (true)
				Thread.Sleep(250);
		}

		private static void BuildConsumer<T>(IServiceCollection services, string clientType, Action<TwinoConnectorBuilder> configure)
		{
			services.AddTwinoBus<T>(tmq =>
			{
				tmq.AddHost("tmq://localhost:22201");
				tmq.SetClientType(clientType);
				tmq.OnConnected(connector => Console.WriteLine($"CONNECTED => {clientType}"));
				configure(tmq);
			});
		}

		private static IServiceProvider RunConsumer<T>(IServiceCollection services)
		{
			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus<T>();
			return provider;
		}
	}
}