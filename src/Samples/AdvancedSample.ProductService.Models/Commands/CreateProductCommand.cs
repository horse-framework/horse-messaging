using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.ProductService.Models.Commands
{
	[RouterName(ServiceRoutes.PRODUCT_COMMAND_SERVICE)]
	[DirectContentType(1000)]
	public class CreateProductCommand : ServiceCommand
	{
		public string Name { get; set; }
		public decimal Price { get; set; }
	}
}