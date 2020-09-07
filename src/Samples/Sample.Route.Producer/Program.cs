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
			await Task.Delay(1000);
			var bus = provider.GetService<ITwinoRouteBus>();


			ManualResetEventSlim re = new ManualResetEventSlim(false);
			Task t1 = Task.Run(async () =>
			{
				Console.WriteLine("t1 waiting");
				re.Wait();
				Console.WriteLine("t1 run");
				var result = await bus.PublishRequestJson<SampleARequest, List<SampleResult>>(new SampleARequest {Name = "A1"});
			});
			Task t2 = Task.Run(async () =>
			{
				Console.WriteLine("t2 waiting");
				re.Wait();
				Console.WriteLine("t2 run");
				var result = await bus.PublishRequestJson<SampleARequest, List<SampleResult>>(new SampleARequest {Name = "A2"});
			});

			re.Set();
			Console.ReadLine();
		}
	}
}