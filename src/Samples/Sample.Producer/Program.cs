using System;
using System.Threading.Tasks;
using Twino.Client.TMQ.Bus;
using Twino.Extensions.ConsumerFactory;
using Twino.Ioc;
using Twino.Protocols.TMQ;

namespace Sample.Producer
{
    class Program
    {
        public static IServiceContainer Services { get; } = new ServiceContainer();

        static async Task Main(string[] args)
        {
            Services.UseTwinoBus(t => t.AddHost("tmq://localhost:26222")
                                       .SetClientName("producer")
                                       .SetClientType("sample")
                                       .UseNewtonsoftJsonSerializer());

            ITwinoQueueBus queueBus = Services.Get<ITwinoQueueBus>();

            ModelA a = new ModelA();
            a.Foo = "foo";
            a.No = 123;

            while (true)
            {
                TwinoResult result = await queueBus.PushJson(a);
                Console.WriteLine($"Push: {result.Code}");
                await Task.Delay(5000);
            }
        }
    }
}