using System.Threading.Tasks;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.ProductService.Core.BusinessManagers.Interfaces;
using AdvancedSample.ProductService.Domain;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using AutoMapper;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.ProductService.Core.BusinessManagers
{
	internal class ProductBusinessManager : IProductBusinessManager
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public ProductBusinessManager(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		public async ValueTask<EntityEntry<Product>> Create(ProductDTO product)
		{
			Product entity = _mapper.Map<Product>(product);
			EntityEntry<Product> entry = await _uow.Command<Product>().AddAsync(entity);
			return entry;
		}
	}
}