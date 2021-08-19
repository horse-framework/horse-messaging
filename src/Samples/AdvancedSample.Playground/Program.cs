using System;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Routers;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection service = new ServiceCollection();
service.AddHorseBus(builder => builder.SetHost("horse://localhost:15500")
									  .SetClientType("playground")
									  .UseNewtonsoftJsonSerializer());
IServiceProvider provider = service.BuildServiceProvider();
provider.UseHorseBus();
IHorseRouterBus bus = provider.GetRequiredService<IHorseRouterBus>();

while (true)
{
	var result = await bus.PublishJson(ServiceRoutes.PRODUCT_COMMAND_SERVICE, new { Name = "Deneme", Price = 30M },1000, true);
	Console.WriteLine(result.Code);
	Console.ReadLine();
}

internal class Startup { };