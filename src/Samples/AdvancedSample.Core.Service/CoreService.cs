using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedSample.Core.Service
{

	public class CoreService 
	{
		private readonly HostBuilder _hostBuilder = new();
		private readonly ServiceProviderFactory _serviceProviderFactory;

		public CoreService(string clientType)
		{
			if (clientType is null) throw new NullReferenceException("Client type cannot be null");
			_serviceProviderFactory = new ServiceProviderFactory(clientType);
			_hostBuilder.UseServiceProviderFactory(_serviceProviderFactory);
		}

		public void ConfigureServices(Action<IServiceCollection> services) => _hostBuilder.ConfigureServices(services);
		public void AddTransientHandlers<T>() => _serviceProviderFactory.AddTransientHandlers<T>();
		
		public void Run() => _hostBuilder.Build().Run();
	}
}