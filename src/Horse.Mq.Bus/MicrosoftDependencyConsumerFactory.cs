using System;
using System.Threading.Tasks;
using Horse.Mq.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Mq.Bus
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

		public void CreateScope()
		{
			if (_lifetime == ServiceLifetime.Scoped)
				_scope = Provider.CreateScope();
		}

		public Task<object> CreateConsumer(Type consumerType)
		{
			if (_lifetime == ServiceLifetime.Scoped)
				return Task.FromResult(_scope.ServiceProvider.GetService(consumerType));

			object consumer = Provider.GetService(consumerType);
			return Task.FromResult(consumer);
		}

		public Task<IHorseMessageInterceptor> CreateInterceptor(Type interceptorType)
		{
			if (_lifetime == ServiceLifetime.Scoped)
				return Task.FromResult((IHorseMessageInterceptor) _scope.ServiceProvider.GetService(interceptorType));

			object interceptor = Provider.GetService(interceptorType);
			return Task.FromResult((IHorseMessageInterceptor) interceptor);
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