using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.OrderService.Models.Commands
{
	[RouterName(ServiceRoutes.ORDER_COMMAND_SERVICE)]
	[DirectContentType(1000)]
	public class CreateOrderCommand : ServiceCommand
	{
		public int ProductId { get; set; }
		public int Quantity { get; set; }
	}
}