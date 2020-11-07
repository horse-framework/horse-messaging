using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Twino.MQ.Client;

namespace Twino.MQ.Bus
{
    internal class MicrosoftDependencyConsumerFactory : IConsumerFactory
    {
        private readonly ServiceLifetime _lifetime;
        internal static IServiceProvider Provider { get; set; }
        private IServiceScope _scope;

        public MicrosoftDependencyConsumerFactory(ServiceLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        public Task<object> CreateConsumer(Type consumerType)
        {
            if (_lifetime == ServiceLifetime.Scoped)
            {
                _scope = Provider.CreateScope();
                return Task.FromResult(_scope.ServiceProvider.GetService(consumerType));
            }

            object consumer = Provider.GetService(consumerType);
            return Task.FromResult(consumer);
        }

        public void Consumed(Exception error)
        {
            if (_scope != null)
            {
                _scope.Dispose();
                _scope = null;
            }
        }
    }
}