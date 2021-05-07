using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.DirectContentTypes;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.OrderService.Models.Queries
{
	[RouterName(ServiceRoutes.ORDER_QUERY_SERVICE)]
	[DirectContentType(DirectContentTypes.Order.GET_ORDER)]
	public class GetOrderQuery : ServiceQuery
	{
		public int OrderId { get; set; }
	}

	public class GetOrderQueryResult
	{
		public OrderDTO Item { get; }

		public GetOrderQueryResult(OrderDTO item)
		{
			Item = item;
		}
	}
}