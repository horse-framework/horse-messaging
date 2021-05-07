using System.Collections.Generic;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.DirectContentTypes;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.OrderService.Models.Queries
{
	[RouterName(ServiceRoutes.ORDER_QUERY_SERVICE)]
	[DirectContentType(DirectContentTypes.Order.GET_ALL_ORDER_SNAPSHOTS)]
	public class GetAllOrderSnapshotsQuery : ServiceQuery { }

	public class GetAllOrderSnapshotsQueryResult
	{
		public IEnumerable<OrderSnapshotDTO> Items { get; }

		public GetAllOrderSnapshotsQueryResult(IEnumerable<OrderSnapshotDTO> items)
		{
			Items = items;
		}
	}
}