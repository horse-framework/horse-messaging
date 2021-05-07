using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.Commands;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AdvancedSample.OrderService.Models.Queries;
using AdvancedSample.ProductService.Domain;
using AutoMapper;

namespace AdvancedSample.OrderService.Core.Mappers
{
	public class OrderMapperProfile : Profile
	{
		public OrderMapperProfile()
		{
			MapCommands();
			MapQueryResults();
			MapDataTransferObjects();
		}

		public void MapCommands()
		{
			CreateMap<CreateOrderCommand, OrderDTO>();
		}

		public void MapQueryResults()
		{
			CreateMap<GetOrderQueryResult, OrderDTO>();
			CreateMap<GetProductQueryResult, ProductDTO>();
		}

		public void MapDataTransferObjects()
		{
			CreateMap<ProductDTO, Product>();
			CreateMap<Product, ProductDTO>();
			CreateMap<OrderDTO, Order>()
			   .ForMember(m => m.Status, (src) => src.MapFrom(m => (Status) m.Status));
			CreateMap<Order, OrderDTO>()
			   .ForMember(m => m.Status, (src) => src.MapFrom(m => (int) m.Status));

			// snapshots
			CreateMap<OrderSnapshot, OrderSnapshotDTO>();
		}
	}
}