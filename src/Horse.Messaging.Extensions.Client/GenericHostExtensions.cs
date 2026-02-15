using System;
using System.Collections.Generic;
using Horse.Messaging.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Horse messaging client extensions
/// </summary>
public static class GenericHostExtensions
{
    /// <param name="hostBuilder">IHostBuilder</param>
    extension(IHostBuilder hostBuilder)
    {
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want. Default is true.</param>
        /// <returns></returns>
        public IHostBuilder AddHorse(Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(null, configureDelegate, autoConnect);
        }
        
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="key">Service key for keyed services</param>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(key, configureDelegate, autoConnect);
        }

        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(null, configureDelegate, autoConnect);
        }

        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="key">Service key for keyed services</param>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostBuilder AddHorse(string key, Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(key, configureDelegate, autoConnect);
        }
        
        private IHostBuilder AddHorseInternal(string key, Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.UseServiceProviderFactory((hostContext) => new HorseServiceProviderFactory(key, hostContext.Configuration, hostContext.HostingEnvironment, configureDelegate, null, autoConnect));

        }
        
        private IHostBuilder AddHorseInternal(string key, Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.UseServiceProviderFactory((hostContext) => new HorseServiceProviderFactory(key, hostContext.Configuration, hostContext.HostingEnvironment, null, configureDelegate, autoConnect));
        }
    }

    /// <param name="hostBuilder">IHostBuilder</param>
    extension(IHostApplicationBuilder hostBuilder)
    {
      
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(null, configureDelegate, autoConnect);
        }
        
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="key">Service key for keyed services</param>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostApplicationBuilder AddHorse(string key, Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(key, configureDelegate, autoConnect);
        }
        
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostApplicationBuilder AddHorse(Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(null, configureDelegate, autoConnect);
        }
        
        /// <summary>
        /// Uses Horse Messaging Client
        /// </summary>
        /// <param name="key">Service key for keyed services</param>
        /// <param name="configureDelegate">Horse configuration action</param>
        /// <param name="autoConnect">If true, horse client connects when the host starts. If false, you should call UseHorse manually when you want.</param>
        /// <returns></returns>
        public IHostApplicationBuilder AddHorse(string key,Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            return hostBuilder.AddHorseInternal(key, configureDelegate, autoConnect);
        }
        
        private IHostApplicationBuilder AddHorseInternal(string key, Action<HorseClientBuilder> configureDelegate, bool autoConnect = true)
        {
            HorseServiceProviderFactory factory = new(key, hostBuilder.Configuration, hostBuilder.Environment, configureDelegate, null, autoConnect);
            hostBuilder.ConfigureContainer(factory);
            return hostBuilder;
        }
        
        private IHostApplicationBuilder AddHorseInternal(string key, Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> configureDelegate, bool autoConnect = true)
        {
            HorseServiceProviderFactory factory = new(key, hostBuilder.Configuration, hostBuilder.Environment, null, configureDelegate, autoConnect);
            hostBuilder.ConfigureContainer(factory);
            return hostBuilder;
        }
    }

    extension(IHost host)
    {
        /// <summary>
        /// The host will connect to the server and start horse bus. You should call that method if you set autoConnect to false in AddHorse method.
        /// </summary>
        /// <returns></returns>
        public IHost UseHorse()
        {
            host.Services.UseHorse();
            return host;
        }
        
        /// <summary>
        /// The host will connect to the server and start horse bus. You should call that method if you set autoConnect to false in AddHorse method.
        /// </summary>
        /// <param name="key">Keyed services key</param>
        /// <returns></returns>
        public IHost UseHorse(string key)
        {
            host.Services.UseHorse(key);
            return host;
        }
    }
}