using System;
using Horse.Messaging.Client.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client.Internal;

/// <summary>
/// Represents a provided service object with it's scope
/// </summary>
public class ProvidedHandler
{
    /// <summary>
    /// The scope service is provided
    /// </summary>
    private IServiceScope Scope { get; set; }
        
    /// <summary>
    /// Service object's itself
    /// </summary>
    public object Service { get; }

    /// <summary>
    /// Creates new provided handler
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="service"></param>
    public ProvidedHandler(IServiceScope scope, object service)
    {
        Scope = scope;
        Service = service;
    }

    /// <summary>
    /// Disposes the object and scope if exists
    /// </summary>
    public void Dispose()
    {
        if (Scope == null) return;
        Scope.Dispose();
        Scope = null;
    }
}

internal class MicrosoftDependencyHandlerFactory(HorseClient client, ServiceLifetime lifetime) : IHandlerFactory
{
    private IServiceScope _scope;

    public ProvidedHandler CreateHandler(Type consumerType)
    {
        if (lifetime != ServiceLifetime.Scoped) return new ProvidedHandler(null, client.Provider.GetRequiredService(consumerType));
        _scope = client.Provider.CreateScope();
        return new ProvidedHandler(_scope, _scope.ServiceProvider.GetRequiredService(consumerType));
    }

    public IHorseInterceptor CreateInterceptor(Type interceptorType)
    {
        if (lifetime == ServiceLifetime.Scoped)
            return (IHorseInterceptor) _scope.ServiceProvider.GetService(interceptorType);

        object interceptor = _scope.ServiceProvider.GetService(interceptorType);
        return (IHorseInterceptor) interceptor;
    }
}