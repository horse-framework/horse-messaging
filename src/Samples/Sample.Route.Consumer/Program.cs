using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Twino.Extensions.ConsumerFactory;

namespace Sample.Route.Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            BuildConsumer(services, "sample-a-consumer", cfg => { cfg.AddTransientConsumer<SampleARequestHandler>(); });

            var provider = services.BuildServiceProvider();
            provider.UseTwinoBus();

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
    }
}