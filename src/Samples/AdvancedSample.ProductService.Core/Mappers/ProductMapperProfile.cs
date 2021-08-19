using AdvancedSample.ProductService.Domain;
using AdvancedSample.ProductService.Models.Commands;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using AutoMapper;

namespace AdvancedSample.ProductService.Core.Mappers
{
	public class ProductMapperProfile : Profile
	{
		public ProductMapperProfile()
		{
			MapCommands();
			MapDataTransferObjects();
		}

		public void MapCommands()
		{
			CreateMap<CreateProductCommand, ProductDTO>();
		}

		public void MapDataTransferObjects()
		{
			CreateMap<ProductDTO, Product>();
			CreateMap<Product, ProductDTO>();
		}
	}
}