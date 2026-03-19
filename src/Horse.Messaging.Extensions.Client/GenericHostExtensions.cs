using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Extension methods for integrating Horse Messaging Client with the .NET Generic Host
/// (<see cref="IHostBuilder"/>, <see cref="IHostApplicationBuilder"/> and <see cref="IHost"/>).
/// <para>
/// These overloads forward to <see cref="HorseClientExtensions"/> on
/// <see cref="IServiceCollection"/> and exist purely for ergonomics when composing the host.
/// </para>
/// </summary>
public static class GenericHostExtensions
{
    // ─────────────────────────────────────────────────────────────────────────
    // IHostBuilder  (Host.CreateDefaultBuilder / WebHost.CreateDefaultBuilder)
    // ─────────────────────────────────────────────────────────────────────────
    extension(IHostBuilder hostBuilder)
    {
        /// <summary>
        /// Registers a <see cref="HorseClient"/> into the host's DI container.
        /// </summary>
        /// <param name="configure">Delegate to configure the <see cref="HorseClientBuilder"/>.</param>
        /// <param name="autoConnect">
        /// When <c>true</c> (default) the client connects automatically when the host starts.
        /// Set to <c>false</c> and call <c>host.UseHorse()</c> to connect manually.
        /// </param>
        public IHostBuilder AddHorse(Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            hostBuilder.ConfigureServices((_, s) => HorseRegistrar.Add(s, configure, autoConnect));
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/>.</summary>
        /// <param name="key">The DI service key.</param>
        /// <param name="configure">Delegate to configure the builder.</param>
        /// <param name="autoConnect">When <c>true</c> (default) the client connects on host start.</param>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            hostBuilder.ConfigureServices((_, s) => HorseRegistrar.AddKeyed(s, key, configure, autoConnect));
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IConfiguration> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.Configuration), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.Configuration), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IHostEnvironment> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.HostingEnvironment), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IHostEnvironment> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.HostingEnvironment), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (_, s) =>
                HorseRegistrar.Add(s, b => configure(b, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (_, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IHostEnvironment"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IHostEnvironment> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.Configuration, ctx.HostingEnvironment), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IHostEnvironment"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration, IHostEnvironment> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.Configuration, ctx.HostingEnvironment), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.Configuration, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.Configuration, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IHostEnvironment, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.HostingEnvironment, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IHostEnvironment, IServiceCollection> configure, bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.HostingEnvironment, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>, <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configure,
            bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.Add(s, b => configure(b, ctx.Configuration, ctx.HostingEnvironment, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>, <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostBuilder AddHorse(
            string key,
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configure,
            bool autoConnect = true)
        {
            Action<HostBuilderContext, IServiceCollection> reg = (ctx, s) =>
                HorseRegistrar.AddKeyed(s, key, b => configure(b, ctx.Configuration, ctx.HostingEnvironment, s), autoConnect);
            hostBuilder.ConfigureServices(reg);
            return hostBuilder;
        }

        /// <summary>Registers a typed <see cref="HorseClient{TIdentifier}"/> into the host's DI container.</summary>
        /// <typeparam name="TIdentifier">Marker type used to distinguish this connection.</typeparam>
        public IHostBuilder AddHorse<TIdentifier>(Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            hostBuilder.ConfigureServices((_, s) => HorseRegistrar.Add<TIdentifier>(s, configure, autoConnect));
            return hostBuilder;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IHostApplicationBuilder  (WebApplication.CreateBuilder / Host.CreateApplicationBuilder)
    // ─────────────────────────────────────────────────────────────────────────
    extension(IHostApplicationBuilder hostBuilder)
    {
        /// <summary>
        /// Registers a <see cref="HorseClient"/> into the host application's DI container.
        /// </summary>
        /// <param name="configure">Delegate to configure the <see cref="HorseClientBuilder"/>.</param>
        /// <param name="autoConnect">When <c>true</c> (default) the client connects on host start.</param>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, configure, autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, configure, autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IConfiguration> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Configuration), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Configuration), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IHostEnvironment> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Environment), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IHostEnvironment> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Environment), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IHostEnvironment"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IHostEnvironment> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Configuration, hostBuilder.Environment), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IHostEnvironment"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration, IHostEnvironment> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Configuration, hostBuilder.Environment), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Configuration, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Configuration, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IHostEnvironment, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add(hostBuilder.Services, b => configure(b, hostBuilder.Environment, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder, IHostEnvironment, IServiceCollection> configure, bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(hostBuilder.Services, key, b => configure(b, hostBuilder.Environment, hostBuilder.Services), autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>, <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configure,
            bool autoConnect = true)
        {
            HorseRegistrar.Add(
                hostBuilder.Services,
                b => configure(b, hostBuilder.Configuration, hostBuilder.Environment, hostBuilder.Services),
                autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a keyed <see cref="HorseClient"/> with access to <see cref="IConfiguration"/>, <see cref="IHostEnvironment"/> and <see cref="IServiceCollection"/>.</summary>
        public IHostApplicationBuilder AddHorse(
            string key,
            Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configure,
            bool autoConnect = true)
        {
            HorseRegistrar.AddKeyed(
                hostBuilder.Services,
                key,
                b => configure(b, hostBuilder.Configuration, hostBuilder.Environment, hostBuilder.Services),
                autoConnect);
            return hostBuilder;
        }

        /// <summary>Registers a typed <see cref="HorseClient{TIdentifier}"/> into the host application's DI container.</summary>
        /// <typeparam name="TIdentifier">Marker type used to distinguish this connection.</typeparam>
        public IHostApplicationBuilder AddHorse<TIdentifier>(Action<HorseClientBuilder> configure, bool autoConnect = true)
        {
            HorseRegistrar.Add<TIdentifier>(hostBuilder.Services, configure, autoConnect);
            return hostBuilder;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IHost  —  Use* (manual activation when autoConnect = false)
    // ─────────────────────────────────────────────────────────────────────────
    extension(IHost host)
    {
        /// <summary>
        /// Connects the registered <see cref="HorseClient"/> to the server.
        /// Only needed when <c>autoConnect = false</c> was passed to <c>AddHorse</c>.
        /// </summary>
        public IHost UseHorse()
        {
            host.Services.UseHorse();
            return host;
        }

        /// <summary>
        /// Connects the keyed <see cref="HorseClient"/> registered under <paramref name="key"/>.
        /// Only needed when <c>autoConnect = false</c> was passed to <c>AddHorse</c>.
        /// </summary>
        /// <param name="key">The DI service key used in <c>AddHorse(key, ...)</c>.</param>
        public IHost UseHorse(string key)
        {
            host.Services.UseHorse(key);
            return host;
        }

        /// <summary>
        /// Connects the typed <see cref="HorseClient{TIdentifier}"/>.
        /// Only needed when <c>autoConnect = false</c> was passed to <c>AddHorse&lt;TIdentifier&gt;</c>.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used in <c>AddHorse&lt;TIdentifier&gt;</c>.</typeparam>
        public IHost UseHorse<TIdentifier>()
        {
            host.Services.UseHorse<TIdentifier>();
            return host;
        }

        /// <summary>
        /// Connects the keyed typed <see cref="HorseClient{TIdentifier}"/>.
        /// Only needed when <c>autoConnect = false</c> was passed to <c>AddKeyedHorse&lt;TIdentifier&gt;</c>.
        /// </summary>
        /// <typeparam name="TIdentifier">Marker type used in <c>AddKeyedHorse&lt;TIdentifier&gt;</c>.</typeparam>
        /// <param name="key">The DI service key.</param>
        public IHost UseHorse<TIdentifier>(string key)
        {
            host.Services.UseHorse<TIdentifier>(key);
            return host;
        }
    }
}