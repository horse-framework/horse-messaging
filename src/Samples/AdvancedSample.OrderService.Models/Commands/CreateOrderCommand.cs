using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.DirectContentTypes;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.OrderService.Models.Commands
{
	[RouterName(ServiceRoutes.ORDER_COMMAND_SERVICE)]
	[DirectContentType(DirectContentTypes.Order.CREATE_ORDER)]
	public class CreateOrderCommand : ServiceCommand
	{
		public int ProductId { get; set; }
		public int Quantity { get; set; }
	}
}