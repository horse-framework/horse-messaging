using AdvancedSample.Common.Infrastructure.Definitions;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Routing;

namespace AdvancedSample.Messaging.Server
{
	public static class RoutBuilder
	{
		public static void ConfigureRoutes(this RouterRider routerRider)
		{
			routerRider.ConfigureProductServiceRoutes();
		}

		private static void ConfigureProductServiceRoutes(this RouterRider routerRider)
		{
			IRouter commandsRouter = routerRider.Add(ServiceRoutes.PRODUCT_COMMAND_SERVICE, RouteMethod.Distribute);
			DirectBinding commandsBinding = new($"[binding]{ClientTypes.PRODUCT_COMMAND_SERVICE}",
												$"@type:{ClientTypes.PRODUCT_COMMAND_SERVICE}",
												1,
												BindingInteraction.Response,
												RouteMethod.RoundRobin);
			commandsRouter.AddBinding(commandsBinding);

			IRouter queriesRouter = routerRider.Add(ServiceRoutes.PRODUCT_QUERY_SERVICE, RouteMethod.Distribute);
			DirectBinding queriesBinding = new($"[binding]{ClientTypes.PRODUCT_QUERY_SERVICE}",
											   $"@type:{ClientTypes.PRODUCT_QUERY_SERVICE}",
											   1,
											   BindingInteraction.Response,
											   RouteMethod.RoundRobin);
			queriesRouter.AddBinding(queriesBinding);
		}
	}
}