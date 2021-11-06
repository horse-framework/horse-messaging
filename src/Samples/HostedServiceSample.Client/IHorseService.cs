using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HostedServiceSample.Client
{
	public interface IHorseService
	{
		public HorseClient HorseClient { get; }
		public void ConfigureHorseClient(Action<HorseClientBuilder> builderDelegate);
		public void ConfigureHostBuilder(Action<IHostBuilder> hostBuilderDelegate);
		public void ConfigureServices(Action<IServiceCollection> configureDelegate);
		public void ConfigureServices(Action<IServiceCollection, IConfiguration> configureDelegate);
		public void Run();
		public Task RunAsync();
	}
}