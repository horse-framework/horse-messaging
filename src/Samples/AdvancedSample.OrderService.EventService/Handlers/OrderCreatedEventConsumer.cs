using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Extensions;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.OrderService.Core.BusinessManagers.Interfaces;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AdvancedSample.OrderService.Models.Events;
using AdvancedSample.OrderService.Models.Queries;
using Horse.Messaging.Client.Routers;
using Newtonsoft.Json;

namespace AdvancedSample.OrderService.EventService.Handlers
{
	public class OrderCreatedEventConsumer : EventConsumer<OrderCreatedEvent>
	{
		private readonly IOrderBusinessManager _bm;
		private readonly IHorseRouterBus _bus;

		public OrderCreatedEventConsumer(IOrderBusinessManager bm, IHorseRouterBus bus)
		{
			_bm = bm;
			_bus = bus;
		}

		protected override async Task Consume(OrderCreatedEvent @event)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(@event)}");

			OrderDTO order = await GetOrder(@event);
			ProductDTO product = await GetProduct(@event, order.ProductId);
			await _bm.CreateSnapshot(order, product);
		}

		private async Task<OrderDTO> GetOrder(OrderCreatedEvent @event)
		{
			GetOrderQuery query = new()
			{
				QueryId = @event.EventId,
				OrderId = @event.OrderId
			};
			GetOrderQueryResult result = await _bus.ExecuteQuery<GetOrderQuery, GetOrderQueryResult>(query);
			return result.Item;
		}

		private async Task<ProductDTO> GetProduct(IServiceEvent @event, int productId)
		{
			GetProductQuery query = new()
			{
				QueryId = @event.EventId,
				ProductId = productId
			};
			GetProductQueryResult result = await _bus.ExecuteQuery<GetProductQuery, GetProductQueryResult>(query);
			return result.Item;
		}
	}
}