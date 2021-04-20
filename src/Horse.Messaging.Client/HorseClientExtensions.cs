using System;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client
{
    //todo: use tidentifier

    /// <summary>
    /// Horse Connector implementations
    /// </summary>
    public static class HorseClientExtensions
    {
        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public static IServiceCollection AddHorseBus(this IServiceCollection services, Action<HorseClientBuilder> config)
        {
            HorseClientBuilder builder = new HorseClientBuilder(services);
            config(builder);
            HorseClient client = builder.Build();
            services.AddSingleton(client);
            
            /* todo: register bus
            services.AddSingleton(connector.Bus);
            services.AddSingleton(connector.Bus.Direct);
            services.AddSingleton(connector.Bus.Queue);
            services.AddSingleton(connector.Bus.Route);
            */
            
            return services;
        }

        /// <summary>
        /// Uses horse bus and connects to the server
        /// </summary>
        public static IServiceProvider UseHorseBus(this IServiceProvider provider)
        {
            HorseClient client = provider.GetService<HorseClient>();
            client.Provider = provider;
            client.Connect();
            return provider;
        }
    }
}