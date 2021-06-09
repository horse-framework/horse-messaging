using System;
using Horse.Mq.Bus;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace RoutingSample.QueueConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			IServiceCollection services = new ServiceCollection();
			services.AddScoped<IDenemeSession, DenemeSession>();
			services.AddHorseBus(hmq =>
								 {
									 hmq.AddHost("hmq://localhost:15500");
									 hmq.AddScopedConsumers(typeof(Program));
									 hmq.AddScopedInterceptors(typeof(Program));
									 hmq.UseNewtonsoftJsonSerializer();
									 hmq.OnConnected(m => Console.WriteLine("CONNECTED"));
									 hmq.OnDisconnected(m => Console.WriteLine("DISCONNECTED"));
								 });

			IServiceProvider provider = services.BuildServiceProvider();
			provider.UseHorseBus();
		

			while (true)
				Console.ReadLine();
		}
	}
}