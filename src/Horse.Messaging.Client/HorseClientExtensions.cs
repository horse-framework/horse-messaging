using System;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Routers;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client;

/// <summary>
/// Horse Connector implementations
/// </summary>
public static class HorseClientExtensions
{
    /// <summary>
    /// Adds Horse connector with configuration
    /// </summary>
    public static IServiceCollection AddHorseBus<TIdentifier>(this IServiceCollection services, Action<HorseClientBuilder> config)
    {
        HorseClientBuilder<TIdentifier> builder = new HorseClientBuilder<TIdentifier>(services);
        config(builder);
        HorseClient<TIdentifier> client = builder.Build();
        services.AddSingleton(client);

        services.AddSingleton<IHorseCache<TIdentifier>>((HorseCache<TIdentifier>) client.Cache);
        services.AddSingleton<IHorseChannelBus<TIdentifier>>(new HorseChannelBus<TIdentifier>(client));
        services.AddSingleton<IHorseQueueBus<TIdentifier>>(new HorseQueueBus<TIdentifier>(client));
        services.AddSingleton<IHorseRouterBus<TIdentifier>>(new HorseRouterBus<TIdentifier>(client));
        services.AddSingleton<IHorseDirectBus<TIdentifier>>(new HorseDirectBus<TIdentifier>(client));

        return services;
    }

    /// <summary>
    /// Adds Horse connector with configuration
    /// </summary>
    public static IServiceCollection AddHorseBus(this IServiceCollection services, Action<HorseClientBuilder> config)
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

        return services;
    }

    /// <summary>
    /// Uses horse bus and connects to the server
    /// </summary>
    public static IServiceProvider UseHorseBus(this IServiceProvider provider)
    {
        HorseClient client = provider.GetRequiredService<HorseClient>();
        client.Provider = provider;
        client.Connect();
        return provider;
    }
}