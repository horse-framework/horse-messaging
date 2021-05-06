using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.DirectContentTypes;
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

		public static Task SaveToOutbox<TEvent>(this IHorseRouterBus bus, TEvent @event) where TEvent : IServiceEvent
		{
			return bus.TryPublishJson(ServiceRoutes.OUTBOX_COMMAND_SERVICE, DirectContentTypes.Outbox.SAVE_TO_OUTBOX, @event);
		}
	}
}