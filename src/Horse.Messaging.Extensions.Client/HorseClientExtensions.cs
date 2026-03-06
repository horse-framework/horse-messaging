using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Extension methods for registering and activating Horse Messaging Client in the .NET DI container.
/// <para>
/// <b>Add*</b> methods register the <see cref="HorseClient"/> and its bus abstractions.<br/>
/// <b>Use*</b> methods activate an already-registered client (connect to the server).
/// </para>
/// </summary>
public static class HorseClientExtensions
{
    // ─────────────────────────────────────────────────────────────────────────
    // IServiceCollection — Add* (registration)
    // ─────────────────────────────────────────────────────────────────────────
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a <see cref="HorseClient"/> and its bus abstractions.
        /// The client is <b>not</b> connected automatically. Call <c>provider.UseHorse()</c>
        /// after building the service provider to connect.
        /// For automatic connection on host start, use the <see cref="IHostBuilder"/> or
        /// <see cref="IHostApplicationBuilder"/> overloads instead.
        /// </summary>
        /// <param name="configure">Delegate to configure the <see cref="HorseClientBuilder"/>.</param>
        public IServiceCollection AddHorse(Action<HorseClientBuilder> configure)
        {
            HorseRegistrar.Add(services, configure, false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IConfiguration> configure,
            IConfiguration configuration = null)
        {
            HorseRegistrar.Add(services, b => configure(b, configuration), false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IHostEnvironment> configure,
            IHostEnvironment environment = null)
        {
            HorseRegistrar.Add(services, b => configure(b, environment), false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IServiceCollection"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IServiceCollection> configure)
        {
            HorseRegistrar.Add(services, b => configure(b, services), false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IHostEnvironment"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment> configure,
            IConfiguration configuration = null,
            IHostEnvironment environment = null)
        {
            HorseRegistrar.Add(services, b => configure(b, configuration, environment), false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IServiceCollection"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IConfiguration, IServiceCollection> configure,
            IConfiguration configuration = null)
        {
            HorseRegistrar.Add(services, b => configure(b, configuration, services), false);
            return services;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IHostEnvironment, IServiceCollection> configure,
            IHostEnvironment environment = null)
        {
            HorseRegistrar.Add(services, b => configure(b, environment, services), false);
            return services;
        }

        /// <summary>
        /// Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>,
        /// <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/> during configuration.
        /// </summary>
        /// <param name="configure">Delegate that receives all host configuration objects.</param>
        /// <param name="configuration">Optional <see cref="IConfiguration"/> instance.</param>
        /// <param name="environment">Optional <see cref="IHostEnvironment"/> instance.</param>
        public IServiceCollection AddHorse(
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configure,
            IConfiguration configuration = null,
            IHostEnvironment environment = null)
        {
            HorseRegistrar.Add(services, b => configure(b, configuration, environment, services), false);
            return services;
        }

        /// <summary>
        /// Registers a typed <see cref="HorseClient{TIdentifier}"/> and its typed bus abstractions.
        /// Use this when multiple independent Horse connections exist in the same application.
        /// The client is <b>not</b> connected automatically.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used to distinguish this connection.</typeparam>
        /// <param name="configure">Delegate to configure the builder.</param>
        public IServiceCollection AddHorse<TIdentifier>(Action<HorseClientBuilder> configure)
        {
            HorseRegistrar.Add<TIdentifier>(services, configure, false);
            return services;
        }

        /// <summary>
        /// Registers a keyed <see cref="HorseClient"/> using .NET keyed-services.
        /// Retrieve it via <c>provider.GetRequiredKeyedService&lt;HorseClient&gt;(key)</c>.
        /// The client is <b>not</b> connected automatically.
        /// </summary>
        /// <param name="key">The DI service key.</param>
        /// <param name="configure">Delegate to configure the builder.</param>
        public IServiceCollection AddKeyedHorse(string key, Action<HorseClientBuilder> configure)
        {
            HorseRegistrar.AddKeyed(services, key, configure, false);
            return services;
        }

        /// <summary>
        /// Registers a keyed typed <see cref="HorseClient{TIdentifier}"/> using .NET keyed-services.
        /// The client is <b>not</b> connected automatically.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used to distinguish this connection.</typeparam>
        /// <param name="key">The DI service key.</param>
        /// <param name="configure">Delegate to configure the builder.</param>
        public IServiceCollection AddKeyedHorse<TIdentifier>(string key, Action<HorseClientBuilder> configure)
        {
            HorseRegistrar.AddKeyed<TIdentifier>(services, key, configure, false);
            return services;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IServiceProvider — Use* (activation / connect)
    // ─────────────────────────────────────────────────────────────────────────
    extension(IServiceProvider provider)
    {
        /// <summary>
        /// Resolves the registered <see cref="HorseClient"/>, injects the service provider
        /// and connects to the Horse server.
        /// This is the standard way to connect when using <see cref="IServiceCollection"/> overloads
        /// of <c>AddHorse</c>, which do not auto-connect.
        /// </summary>
        public IServiceProvider UseHorse()
        {
            HorseClient client = provider.GetRequiredService<HorseClient>();
            client.Provider = provider;
            client.Connect();
            return provider;
        }

        /// <summary>
        /// Resolves the keyed <see cref="HorseClient"/> and connects.
        /// </summary>
        /// <param name="key">The DI service key used in <c>AddKeyedHorse</c>.</param>
        public IServiceProvider UseHorse(string key)
        {
            HorseClient client = provider.GetRequiredKeyedService<HorseClient>(key);
            client.Provider = provider;
            client.Connect();
            return provider;
        }

        /// <summary>
        /// Resolves the typed <see cref="HorseClient{TIdentifier}"/> and connects.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used in <c>AddHorse&lt;TIdentifier&gt;</c>.</typeparam>
        public IServiceProvider UseHorse<TIdentifier>()
        {
            HorseClient<TIdentifier> client = provider.GetRequiredService<HorseClient<TIdentifier>>();
            client.Provider = provider;
            client.Connect();
            return provider;
        }

        /// <summary>
        /// Resolves the keyed typed <see cref="HorseClient{TIdentifier}"/> and connects.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used in <c>AddKeyedHorse&lt;TIdentifier&gt;</c>.</typeparam>
        /// <param name="key">The DI service key.</param>
        public IServiceProvider UseHorse<TIdentifier>(string key)
        {
            HorseClient<TIdentifier> client = provider.GetRequiredKeyedService<HorseClient<TIdentifier>>(key);
            client.Provider = provider;
            client.Connect();
            return provider;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helper — called by HorseRegistrar
    // ─────────────────────────────────────────────────────────────────────────

    internal static void RegisterConnectAndShutdown(IServiceCollection services, string serviceKey, HorseClient client, bool autoConnect)
    {
        if (autoConnect)
            services.AddHostedService(prov => new HorseConnectService(prov, serviceKey));

        if (client.GracefulShutdownOptions == null) return;

        services.AddHostedService(prov => new GracefulShutdownService(prov, serviceKey));
    }
}