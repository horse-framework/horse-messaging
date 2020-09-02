using System;
using Twino.Extensions.ConsumerFactory;
using Twino.Ioc;

namespace Sample.Consumer
{
    class Program
    {
        public static IServiceContainer Services { get; } = new ServiceContainer();

        static void Main(string[] args)
        {
            Services.UseTwinoBus(t => t.AddHost("tmq://localhost:26222")
                                       .SetClientName("consumer")
                                       .SetClientType("sample")
                                       //.AddTransientConsumers(typeof(Program))
                                       .AddTransientConsumer<ModelAConsumer>()
                                       .UseNewtonsoftJsonSerializer());

            while (true)
                Console.ReadLine();
        }
    }
}