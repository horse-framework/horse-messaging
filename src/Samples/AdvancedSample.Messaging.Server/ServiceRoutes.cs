using AdvancedSample.Common.Infrastructure.Definitions;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Routing;

namespace AdvancedSample.Messaging.Server
{
	public static class ServiceRoutes
	{
		public static void ConfigureRoutes(this RouterRider routerRider)
		{
			routerRider.ConfigureProductServiceRoutes();
		}

		private static void ConfigureProductServiceRoutes(this RouterRider routerRider)
		{
			IRouter router = routerRider.Add(Common.Infrastructure.Definitions.ServiceRoutes.PRODUCT_COMMAND_SERVICE, RouteMethod.Distribute);
			DirectBinding directBinding = new($"[binding]{ClientTypes.PRODUCT_COMMAND_SERVICE}",
											  $"@type:{ClientTypes.PRODUCT_COMMAND_SERVICE}",
											  1,
											  BindingInteraction.Response,
											  RouteMethod.RoundRobin);
			router.AddBinding(directBinding);
		}
	}
}