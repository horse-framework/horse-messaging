using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

internal sealed class HorseServiceProviderFactory(
    string keyedServicesKey,
    IConfiguration config,
    IHostEnvironment hostEnvironment,
    Action<HorseClientBuilder> @delegate,
    Action<HorseClientBuilder, IConfiguration, IHostEnvironment, IServiceCollection> delegateWithConfigAndEnvAndServices,
    bool autoConnect = true)
    : IServiceProviderFactory<IServiceCollection>
{

    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        if (string.IsNullOrWhiteSpace(keyedServicesKey))
        {
            services.AddHorse((builder) =>
            {
                @delegate?.Invoke(builder);
                delegateWithConfigAndEnvAndServices?.Invoke(builder, config, hostEnvironment, services);
            });
        }
        else
        {
            services.AddKeyedHorse(keyedServicesKey, (builder) =>
            {
                @delegate?.Invoke(builder);
                delegateWithConfigAndEnvAndServices?.Invoke(builder, config, hostEnvironment, services);
            });
        }
      
        return services;
    }

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        ServiceProvider provider = containerBuilder.BuildServiceProvider();
        if (autoConnect)
        {
            if (string.IsNullOrWhiteSpace(keyedServicesKey))
                provider.UseHorse();
            else
                provider.UseHorse(keyedServicesKey);
        }
        return provider;
    }
}