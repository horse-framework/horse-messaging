using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;
using AdvancedSample.OrderService.Core;

CoreService service = new(ClientTypes.ORDER_COMMAND_SERVICE);
service.ConfigureServices(s =>
						  {
							  s.AddCoreServices();
							  s.AddBusinessManagers();
						  });
service.AddTransientHandlers<Startup>();
service.Run();

internal class Startup { }