using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client.Internal
{
    /// <summary>
    /// Represents a provided service object with it's scope
    /// </summary>
    public class ProvidedHandler
    {
        /// <summary>
        /// The scope service is provided
        /// </summary>
        public IServiceScope Scope { get; private set; }
        
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
            if (Scope != null)
            {
                Scope.Dispose();
                Scope = null;
            }
        }
    }

    internal class MicrosoftDependencyHandlerFactory : IHandlerFactory
    {
        private IServiceScope _scope;
        private readonly ServiceLifetime _lifetime;
        private readonly HorseClient _client;

        public MicrosoftDependencyHandlerFactory(HorseClient client, ServiceLifetime lifetime)
        {
            _client = client;
            _lifetime = lifetime;
        }

        public ProvidedHandler CreateHandler(Type consumerType)
        {
            if (_lifetime == ServiceLifetime.Scoped)
            {
                _scope = _client.Provider.CreateScope();
                return new ProvidedHandler(_scope, _scope.ServiceProvider.GetRequiredService(consumerType));
            }

            return new ProvidedHandler(null, _client.Provider.GetRequiredService(consumerType));
        }

        public IHorseInterceptor CreateInterceptor(Type interceptorType)
        {
            if (_lifetime == ServiceLifetime.Scoped)
                return (IHorseInterceptor) _scope.ServiceProvider.GetService(interceptorType);

            object interceptor = _scope.ServiceProvider.GetService(interceptorType);
            return (IHorseInterceptor) interceptor;
        }
    }
}