using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client.Internal
{
    public class ProvidedHandler
    {
        public IServiceScope Scope { get; private set; }
        public object Service { get; }

        public ProvidedHandler(IServiceScope scope, object service)
        {
            Scope = scope;
            Service = service;
        }

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
                return new ProvidedHandler(_scope, _scope.ServiceProvider.GetService(consumerType));
            }

            return new ProvidedHandler(null, _client.Provider.GetService(consumerType));
        }
    }
}