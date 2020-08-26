using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sample.Producer.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Extensions.ConsumerFactory;
using Twino.Protocols.TMQ;

namespace Sample.Producer
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddTwinoBus(tmq => { tmq.AddHost("tmq://127.0.0.1:22200"); });
			var provider = services.BuildServiceProvider();
			provider.UseTwinoBus();
			var bus = provider.GetService<ITwinoQueueBus>();

			while (true)
			{
				/*
				ModelC c = new ModelC();
				c.Value = "Hello";
				var result = await bus.PublishRequestJson<ModelC, ModelA>(c);*/
				await bus.PushJson(new ModelA {Foo = "Hello!"}, false);

				await Task.Delay(2500);
			}
		}
	}
}