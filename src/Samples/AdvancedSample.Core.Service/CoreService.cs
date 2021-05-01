using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedSample.Core.Service
{
	public class CoreService<T> where T : notnull, IServiceStartup
	{
		public IRegistrar Registrar => _serviceProviderFactory;

		private readonly HostBuilder _hostBuilder = new();
		private readonly ServiceProviderFactory<T> _serviceProviderFactory;

		public CoreService(string clientType)
		{
			_serviceProviderFactory = new ServiceProviderFactory<T>(clientType);
			_hostBuilder.UseServiceProviderFactory(_serviceProviderFactory);
		}

		public void ConfigureServices(Action<IServiceCollection> services)
		{
			_hostBuilder.ConfigureServices(services);
		}

		public void Start() => _hostBuilder.Build().Run();
	}
}