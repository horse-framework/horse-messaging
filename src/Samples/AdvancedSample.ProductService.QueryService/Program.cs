using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;
using AdvancedSample.ProductService.Core;

CoreService service = new(ClientTypes.PRODUCT_QUERY_SERVICE);
service.ConfigureServices(s => s.AddCoreServices());
service.AddTransientHandlers<Startup>();
service.Run();

internal class Startup { };