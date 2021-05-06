using AutoMapper;

namespace AdvancedSample.OrderService.Core.Mappers
{
	public class OrderMapperProfile : Profile
	{
		public OrderMapperProfile()
		{
			MapCommands();
			MapDataTransferObjects();
		}

		public void MapCommands() { }

		public void MapDataTransferObjects() { }
	}
}