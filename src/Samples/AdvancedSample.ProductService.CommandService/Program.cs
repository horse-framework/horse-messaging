using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.ProductService.Domain;

CoreService service = new(ClientTypes.PRODUCT_COMMAND_SERVICE);
service.ConfigureServices(s => s.AddRepositories<Product>());
service.AddTransientHandlers<Startup>();
service.Run();

internal class Startup { }