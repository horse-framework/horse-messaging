using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Routers;

namespace AdvancedSample.Common.Extensions
{
	public static class HorseRoutePublishExtensions
	{
		public static Task ExecuteCommand<TCommand>(this IHorseRouterBus bus, TCommand command) where TCommand : IServiceCommand
		{
			return bus.TryPublishJson(command);
		}

		public static Task ExecuteCommand<TCommand>(this IHorseRouterBus bus, string routerName, TCommand command) where TCommand : IServiceCommand
		{
			return bus.TryPublishJson(routerName, command);
		}

		public static Task ExecuteCommand<TCommand>(this IHorseRouterBus bus, string routerName, ushort contentType, TCommand command) where TCommand : IServiceCommand
		{
			return bus.TryPublishJson(routerName, contentType, command);
		}

		public static Task<TResult> ExecuteQuery<TQuery, TResult>(this IHorseRouterBus bus, TQuery query) where TQuery : IServiceQuery
		{
			return bus.TryPublishRequestJson<TQuery, TResult>(query);
		}

		public static Task<TResult> ExecuteQuery<TQuery, TResult>(this IHorseRouterBus bus, string routerName, TQuery query) where TQuery : IServiceQuery
		{
			return bus.TryPublishRequestJson<TQuery, TResult>(routerName, query);
		}

		public static Task<TResult> ExecuteQuery<TQuery, TResult>(this IHorseRouterBus bus, string routerName, ushort contentType, TQuery query) where TQuery : IServiceQuery
		{
			return bus.TryPublishRequestJson<TQuery, TResult>(routerName, contentType, query);
		}

		public static Task RaiseEvent(this IHorseQueueBus bus, object @event)
		{
			return bus.TryPushJson(@event);
		}

		public static Task RaiseEvent<T>(this IHorseQueueBus bus, T @event) where T : IServiceEvent
		{
			return bus.TryPushJson(@event);
		}

		public static Task RaiseEvent<T>(this IHorseQueueBus bus, string queueName, T @event) where T : IServiceEvent
		{
			return bus.TryPushJson(queueName, @event);
		}
	}
}