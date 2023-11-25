using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Horse service provider factory for generic host.
/// </summary>
public class HorseServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly HostBuilderContext _builderContext;
    private readonly bool _autoConnect;
    private const string _clientBuilderDelegate = "HorseClientBuilderDelegate";

    /// <summary>
    /// Create new factory
    /// </summary>
    /// <param name="builderContext"></param>
    /// <param name="autoConnect">Connect to server when host is started.</param>
    public HorseServiceProviderFactory(HostBuilderContext builderContext, bool autoConnect = true)
    {
        _builderContext = builderContext;
        _autoConnect = autoConnect;
        if (!builderContext.Properties.ContainsKey(_clientBuilderDelegate))
            throw new InvalidOperationException("Horse client was not configured. You can use ConfigureHorseClient extension method in IHostBuilder.");
    }

    /// <inheritdoc />
    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        if (_builderContext.Properties.ContainsKey("HasHorseClientBuilderDelegateContext"))
        {
            Action<HostBuilderContext, HorseClientBuilder> clientBuilder = (Action<HostBuilderContext, HorseClientBuilder>) _builderContext.Properties[_clientBuilderDelegate];
            services.AddHorseBus((builder) => clientBuilder?.Invoke(_builderContext, builder));
        }
        else
        {
            Action<HorseClientBuilder> clientBuilder = (Action<HorseClientBuilder>) _builderContext.Properties[_clientBuilderDelegate];
            services.AddHorseBus((builder) => clientBuilder?.Invoke(builder));
        }

        return services;
    }

    /// <inheritdoc />
    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        var provider = containerBuilder.BuildServiceProvider();
        if (_autoConnect) provider.UseHorseBus();
        return provider;
    }
}