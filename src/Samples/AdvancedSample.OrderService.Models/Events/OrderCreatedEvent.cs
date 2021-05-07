using System;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using Horse.Messaging.Client.Queues.Annotations;

namespace AdvancedSample.OrderService.Models.Events
{
	[QueueName(ServiceQueues.ORDER_CREATED)]
	public class OrderCreatedEvent : ServiceEvent
	{
		public int OrderId { get; set; }
		private OrderCreatedEvent() { }

		public static OrderCreatedEvent Create(int orderId)
		{
			return new OrderCreatedEvent
			{
				EventId = Guid.NewGuid(),
				OrderId = orderId
			};
		}
	}
}