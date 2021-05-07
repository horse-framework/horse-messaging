using System;
using System.Collections.Generic;
using System.Net;
using AdvancedSample.Core.Api.Middlewares;
using AdvancedSample.Core.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace AdvancedSample.Core.Api
{
	public class CoreApi<T>
	{
		private readonly string _clientType;
		private readonly int _port;
		private readonly HostBuilder _hostBuilder = new();

		public CoreApi(string clientType, int port)
		{
			_clientType = clientType ?? throw new NullReferenceException("Client type cannot be null");
			_port = port;
			ServiceProviderFactory<T> serviceProviderFactory = new(clientType);
			_hostBuilder.UseServiceProviderFactory(serviceProviderFactory);
			Build();
		}

		public void ConfigureServices(Action<IServiceCollection> services)
		{
			_hostBuilder.ConfigureServices(services);
		}

		public void Run()
		{
			_hostBuilder.Build().Run();
		}

		private void Build()
		{
			_hostBuilder.ConfigureWebHostDefaults(ConfigureWebHostDefaults)
						.ConfigureServices(ConfigureServices);
		}

		private void ConfigureWebHostDefaults(IWebHostBuilder builder)
		{
			builder.UseKestrel(opt =>
							   {
								   List<IPAddress> ipAddresses = new();
								   {
									   ipAddresses.Add(IPAddress.IPv6Loopback);
									   ipAddresses.Add(IPAddress.Loopback);
								   }
								   foreach (IPAddress address in ipAddresses) opt.Listen(address, _port);
							   });
			builder.Configure((_, app) =>
							  {
								  app.UseCors(cfg =>
											  {
												  cfg.AllowAnyHeader();
												  cfg.AllowAnyOrigin();
												  cfg.AllowAnyMethod();
											  });
								  app.UseRouting();
								  app.UseMiddleware<ExceptionHandler>();
								  app.UseEndpoints(ep => ep.MapControllers());

								  app.UseSwagger();
								  app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", _clientType); });
							  });
		}

		private void ConfigureServices(IServiceCollection services)
		{
			services.AddMvc();
			services.AddSwaggerGen(c =>
								   {
									   c.SwaggerDoc("v1", new OpenApiInfo
									   {
										   Title = $"{_clientType} API",
										   Version = "v1"
									   });
								   });
		}
	}
}