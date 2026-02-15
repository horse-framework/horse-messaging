using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Routers;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Horse Connector implementations
/// </summary>
public static class HorseClientExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public IServiceCollection AddHorse<TIdentifier>(Action<HorseClientBuilder> config)
        {
            HorseClientBuilder<TIdentifier> builder = new(services);
            config(builder);
            HorseClient<TIdentifier> client = builder.Build();
            services.AddSingleton(client);

            services.AddSingleton<IHorseCache<TIdentifier>>((HorseCache<TIdentifier>)client.Cache);
            services.AddSingleton<IHorseChannelBus<TIdentifier>>(new HorseChannelBus<TIdentifier>(client));
            services.AddSingleton<IHorseQueueBus<TIdentifier>>(new HorseQueueBus<TIdentifier>(client));
            services.AddSingleton<IHorseRouterBus<TIdentifier>>(new HorseRouterBus<TIdentifier>(client));
            services.AddSingleton<IHorseDirectBus<TIdentifier>>(new HorseDirectBus<TIdentifier>(client));

            RegisterGracefulShutdownIfConfigured(services, null, client);
            return services;
        }
        
        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public IServiceCollection AddHorse(Action<HorseClientBuilder> config)
        {
            HorseClientBuilder builder = new(services);
            config(builder);
            HorseClient client = builder.Build();
            services.AddSingleton(client);
            services.AddSingleton(client.Cache);
            services.AddSingleton<IHorseChannelBus>(new HorseChannelBus(client));
            services.AddSingleton<IHorseQueueBus>(new HorseQueueBus(client));
            services.AddSingleton<IHorseRouterBus>(new HorseRouterBus(client));
            services.AddSingleton<IHorseDirectBus>(new HorseDirectBus(client));

            RegisterGracefulShutdownIfConfigured(services, null, client);

            return services;
        }

        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public IServiceCollection AddKeyedHorse(string key, Action<HorseClientBuilder> config)
        {
            HorseClientBuilder builder = new(services);
            builder.ServiceKey = key;
            config(builder);
            HorseClient client = builder.Build();
            services.AddKeyedSingleton(key, client);
            services.AddKeyedSingleton(key, client.Cache);
            services.AddKeyedSingleton<IHorseChannelBus>(key, new HorseChannelBus(client));
            services.AddKeyedSingleton<IHorseQueueBus>(key, new HorseQueueBus(client));
            services.AddKeyedSingleton<IHorseRouterBus>(key, new HorseRouterBus(client));
            services.AddKeyedSingleton<IHorseDirectBus>(key, new HorseDirectBus(client));

            RegisterGracefulShutdownIfConfigured(services, key, client);

            return services;
        }
    }

    private static void RegisterGracefulShutdownIfConfigured(IServiceCollection services, string serviceKey, HorseClient client)
    {
        var options = client.GracefulShutdownOptions;
        if (options == null) return;

        services.AddHostedService(prov => new GracefulShutdownService(
            prov,
            serviceKey,
            options.MinWait,
            options.MaxWait,
            options.ShuttingDownAction,
            options.ShuttingDownActionWithProvider));
    }

    extension(IServiceProvider provider)
    {
        /// <summary>
        /// Uses horse bus and connects to the server
        /// </summary>
        public IServiceProvider UseHorse()
        {
            HorseClient client = provider.GetRequiredService<HorseClient>();
            client.Provider = provider;
            client.Connect();
            return provider;
        }
        
        /// <summary>
        /// Uses horse bus and connects to the server
        /// <param name="key">Keyed services key</param>
        /// </summary>
        public IServiceProvider UseHorse(string key)
        {
            HorseClient client = provider.GetRequiredKeyedService<HorseClient>(key);
            client.Provider = provider;
            client.Connect();
            return provider;
        }
    }
}