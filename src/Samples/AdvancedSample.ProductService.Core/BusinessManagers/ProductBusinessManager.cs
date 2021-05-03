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
		private readonly ICommandRepository<Product> _repository;

		public ProductBusinessManager(IMapper mapper, ICommandRepository<Product> repository)
		{
			_mapper = mapper;
			_repository = repository;
		}

		public ValueTask<EntityEntry<Product>> Create(ProductDTO product)
		{
			Product entity = _mapper.Map<Product>(product);
			return _repository.AddAsync(entity);
		}
	}
}