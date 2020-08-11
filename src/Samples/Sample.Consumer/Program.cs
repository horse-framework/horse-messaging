using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Twino.Client.TMQ.Bus;
using Twino.Client.TMQ.Connectors;
using Twino.Extensions.ConsumerFactory;
using Twino.Protocols.TMQ;

namespace Sample.Consumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddTwinoBus(tmq =>
            {
                tmq.AddHost("tmq://127.0.0.1:22200");
                tmq.SetClientName("consumer");
                tmq.AddTransientConsumers(typeof(Program));
            });
            var provider = services.BuildServiceProvider();
            provider.UseTwinoBus();
            while (true)
                await Task.Delay(1000);
        }
    }
}