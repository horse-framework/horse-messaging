using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.OrderService.Core.BusinessManagers.Interfaces;
using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AdvancedSample.OrderService.Models.Events;
using AutoMapper;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;

namespace AdvancedSample.OrderService.Core.BusinessManagers
{
	internal class OrderBusinessManager : IOrderBusinessManager
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public OrderBusinessManager(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		public async ValueTask<Order> Create(OrderDTO order)
		{
			await using IDbContextTransaction transaction = await _uow.BeginTransaction();
			Order entity = _mapper.Map<Order>(order);
			entity.Status = Status.Waiting;
			EntityEntry<Order> entry = await _uow.Command<Order>().AddAsync(entity);
			await _uow.SaveChangesAsync();
			OrderCreatedEvent @event = OrderCreatedEvent.Create(entity.Id);
			await CreateEvent(@event);
			await _uow.SaveChangesAsync();
			await transaction.CommitAsync();
			return entry.Entity;
		}

		public async ValueTask<OrderSnapshot> CreateSnapshot(OrderDTO order, ProductDTO product)
		{
			OrderSnapshot snapshot = new()
			{
				Id = order.Id,
				Quantity = order.Quantity,
				CreatedAt = order.CreatedAt,
				DeletedAt = order.DeletedAt,
				UpdatedAt = order.UpdatedAt,
				ProductId = product.Id,
				ProductName = product.Name,
				TotalPrice = product.Price * order.Quantity
			};
			EntityEntry<OrderSnapshot> entry = await _uow.Command<OrderSnapshot>().AddAsync(snapshot);
			await _uow.SaveChangesAsync();
			return entry.Entity;
		}

		private async Task CreateEvent(IServiceEvent @event)
		{
			OutboxMessage outboxMessage = new()
			{
				Type = JsonConvert.SerializeObject(@event.GetType()),
				Status = OutboxMessageStatus.Waiting,
				MessageJSON = JsonConvert.SerializeObject(@event)
			};
			await _uow.Command<OutboxMessage>().AddAsync(outboxMessage);
		}
	}
}