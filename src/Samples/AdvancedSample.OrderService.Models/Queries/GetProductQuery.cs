using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.DirectContentTypes;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.OrderService.Models.Queries
{
	[RouterName(ServiceRoutes.PRODUCT_QUERY_SERVICE)]
	[DirectContentType(DirectContentTypes.Product.GET_PRODUCT)]
	public class GetProductQuery : ServiceQuery
	{
		public int ProductId { get; set; }
	}

	public class GetProductQueryResult
	{
		public ProductDTO Item { get;  }

		public GetProductQueryResult(ProductDTO item)
		{
			Item = item;
		}
	}
}