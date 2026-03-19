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
/// Core registration logic extracted into a plain static class so it can be called
/// safely from both C# 13 extension members (which have overload resolution quirks)
/// and regular code paths.
/// </summary>
internal static class HorseRegistrar
{
    internal static void Add(IServiceCollection services, Action<HorseClientBuilder> configure, bool autoConnect)
    {
        HorseClientBuilder builder = new(services);
        configure(builder);
        HorseClient client = builder.Build();

        services.AddSingleton(client);
        services.AddSingleton(client.Cache);
        services.AddSingleton<IHorseChannelBus>(new HorseChannelBus(client));
        services.AddSingleton<IHorseQueueBus>(new HorseQueueBus(client));
        services.AddSingleton<IHorseRouterBus>(new HorseRouterBus(client));
        services.AddSingleton<IHorseDirectBus>(new HorseDirectBus(client));

        HorseClientExtensions.RegisterConnectAndShutdown(services, serviceKey: null, client, autoConnect);
    }

    internal static void Add<TIdentifier>(IServiceCollection services, Action<HorseClientBuilder> configure, bool autoConnect)
    {
        HorseClientBuilder<TIdentifier> builder = new(services);
        configure(builder);
        HorseClient<TIdentifier> client = builder.Build();

        services.AddSingleton(client);
        services.AddSingleton<IHorseCache<TIdentifier>>((HorseCache<TIdentifier>)client.Cache);
        services.AddSingleton<IHorseChannelBus<TIdentifier>>(new HorseChannelBus<TIdentifier>(client));
        services.AddSingleton<IHorseQueueBus<TIdentifier>>(new HorseQueueBus<TIdentifier>(client));
        services.AddSingleton<IHorseRouterBus<TIdentifier>>(new HorseRouterBus<TIdentifier>(client));
        services.AddSingleton<IHorseDirectBus<TIdentifier>>(new HorseDirectBus<TIdentifier>(client));

        HorseClientExtensions.RegisterConnectAndShutdown(services, serviceKey: null, client, autoConnect);
    }

    internal static void AddKeyed(IServiceCollection services, string key, Action<HorseClientBuilder> configure, bool autoConnect)
    {
        HorseClientBuilder builder = new(services)
        {
            ServiceKey = key
        };
        configure(builder);
        HorseClient client = builder.Build();

        services.AddKeyedSingleton(key, client);
        services.AddKeyedSingleton(key, client.Cache);
        services.AddKeyedSingleton<IHorseChannelBus>(key, new HorseChannelBus(client));
        services.AddKeyedSingleton<IHorseQueueBus>(key, new HorseQueueBus(client));
        services.AddKeyedSingleton<IHorseRouterBus>(key, new HorseRouterBus(client));
        services.AddKeyedSingleton<IHorseDirectBus>(key, new HorseDirectBus(client));

        HorseClientExtensions.RegisterConnectAndShutdown(services, key, client, autoConnect);
    }

    internal static void AddKeyed<TIdentifier>(IServiceCollection services, string key, Action<HorseClientBuilder> configure, bool autoConnect)
    {
        HorseClientBuilder<TIdentifier> builder = new(services)
        {
            ServiceKey = key
        };
        configure(builder);
        HorseClient<TIdentifier> client = builder.Build();

        services.AddKeyedSingleton(key, client);
        services.AddKeyedSingleton<IHorseCache<TIdentifier>>(key, (HorseCache<TIdentifier>)client.Cache);
        services.AddKeyedSingleton<IHorseChannelBus<TIdentifier>>(key, new HorseChannelBus<TIdentifier>(client));
        services.AddKeyedSingleton<IHorseQueueBus<TIdentifier>>(key, new HorseQueueBus<TIdentifier>(client));
        services.AddKeyedSingleton<IHorseRouterBus<TIdentifier>>(key, new HorseRouterBus<TIdentifier>(client));
        services.AddKeyedSingleton<IHorseDirectBus<TIdentifier>>(key, new HorseDirectBus<TIdentifier>(client));

        HorseClientExtensions.RegisterConnectAndShutdown(services, key, client, autoConnect);
    }
}

