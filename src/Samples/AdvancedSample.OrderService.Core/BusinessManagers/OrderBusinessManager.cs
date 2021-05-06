using System.Threading.Tasks;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.OrderService.Core.BusinessManagers.Interfaces;
using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AutoMapper;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.OrderService.Core.BusinessManagers
{
	internal class OrderBusinessManager : IOrderBusinessManager
	{
		private readonly IMapper _mapper;
		private readonly ICommandRepository<Order> _repository;

		public OrderBusinessManager(IMapper mapper, ICommandRepository<Order> repository)
		{
			_mapper = mapper;
			_repository = repository;
		}

		public async ValueTask<EntityEntry<Order>> Create(OrderDTO product)
		{
			Order entity = _mapper.Map<Order>(product);
			EntityEntry<Order> entry = await _repository.AddAsync(entity);
			return entry;
		}
	}
}