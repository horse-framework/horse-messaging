using System.Collections.Generic;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.ProductService.Models.Queries
{
	[RouterName(ServiceRoutes.PRODUCT_QUERY_SERVICE)]
	[DirectContentType(1001)]
	public class GetAllProductsQuery : ServiceQuery { }

	public class GetAllProductsQueryResult
	{
		public GetAllProductsQueryResult(IEnumerable<ProductDTO> items)
		{
			Items = items;
		}

		public IEnumerable<ProductDTO> Items { get; set; }
	}
}