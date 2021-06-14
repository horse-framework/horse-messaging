using AdvancedSample.Core;
using Microsoft.Extensions.Hosting;

namespace AdvancedSample.Service
{
	public sealed class SampleService<T> where T : class
	{
		private readonly string _clientType;
		private readonly IHost _host;

		public SampleService(string clientType, string[] args)
		{
			_clientType = clientType;
			_host = BuildHost(args);
		}

		private IHost BuildHost(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
					   .UseServiceProviderFactory(hostContext => new SampleServiceProviderFactory<T>(_clientType, hostContext.Configuration))
					   .ConfigureHostConfiguration(builder => builder.ConfigureHost())
					   .ConfigureAppConfiguration((hostContext, builder) => builder.ConfigureApp(hostContext))
					   .Build();
		}

		public void Run()
		{
			_host.Run();
		}
	}
}