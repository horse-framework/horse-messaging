using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Horse messaging client extensions
/// </summary>
public static class GenericHostExtensions
{
    /// <summary>
    /// Configure your horse client.
    /// That configuration does not enable Horse Client implementation by itself.
    /// You MUST use UseHorse method to complete the implementation.
    /// </summary>
    /// <param name="hostBuilder">IHostBuilder</param>
    /// <param name="configureDelegate">Configure delegate</param>
    public static IHostBuilder ConfigureHorseClient(this IHostBuilder hostBuilder, Action<HostBuilderContext, HorseClientBuilder> configureDelegate)
    {
        hostBuilder.Properties.Add("HasHorseClientBuilderDelegateContext", null);
        return hostBuilder.ConfigureHorseClientInternal(configureDelegate);
    }

    /// <summary>
    /// Adds additional configuration to your Horse Client.
    /// That configuration does not enable Horse Client implementation by itself.
    /// You MUST use UseHorse method to complete the implementation.
    /// </summary>
    /// <param name="hostBuilder">IHostBuilder</param>
    /// <param name="configureDelegate">Configure delegate</param>
    public static IHostBuilder ConfigureHorseClient(this IHostBuilder hostBuilder, Action<HorseClientBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureHorseClientInternal(configureDelegate);
    }

    private static IHostBuilder ConfigureHorseClientInternal(this IHostBuilder hostBuilder, object configureDelegate)
    {
        const string _clientBuilderDelegate = "HorseClientBuilderDelegate";
        if (hostBuilder.Properties.ContainsKey(_clientBuilderDelegate))
            throw new InvalidOperationException("Horse client was already configured.");
        hostBuilder.Properties.Add(_clientBuilderDelegate, configureDelegate);
        return hostBuilder;
    }

    /// <summary>
    /// Uses Horse Messaging Client
    /// </summary>
    /// <param name="host">Builder of Microsoft.Extensions.Hosting</param>
    /// <param name="cfg">Horse configuration action</param>
    /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorseBus manually when you want.</param>
    /// <returns></returns>
    public static IHostBuilder UseHorse(this IHostBuilder host, Action<HorseClientBuilder> cfg, bool autoConnect = true)
    {
        host.ConfigureServices((context, services) =>
        {
            services.AddHorseBus(b =>
            {
                b.AddServices(services);
                cfg(b);
            });

            if (autoConnect)
                services.AddHostedService(p => new HorseRunnerHostedService(p));
        });

        return host;
    }

    /// <summary>
    /// Uses Horse Messaging Client
    /// </summary>
    /// <param name="host">Builder of Microsoft.Extensions.Hosting</param>
    /// <param name="cfg">Horse configuration action</param>
    /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorseBus manually when you want.</param>
    /// <returns></returns>
    public static IHostBuilder UseHorse(this IHostBuilder host, Action<HostBuilderContext, HorseClientBuilder> cfg, bool autoConnect = true)
    {
        host.ConfigureServices((context, services) =>
        {
            services.AddHorseBus(b =>
            {
                b.AddServices(services);
                cfg(context, b);
            });

            if (autoConnect)
                services.AddHostedService(p => new HorseRunnerHostedService(p));
        });
        return host;
    }
}