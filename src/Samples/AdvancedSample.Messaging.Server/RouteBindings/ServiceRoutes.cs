using AdvancedSample.Messaging.Common;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Routing;

namespace AdvancedSample.Messaging.Server.RouteBindings
{
	public static class ServiceRoutes
	{
		public static void ConfigureServiceRoutes(this HorseRider rider)
		{
			IRouter router = rider.Router.Add(AdvancedSampleServiceRoutes.TestService, RouteMethod.Distribute);
			DirectBinding binding = new("test-binding", $"@type:{AdvancedSampleServiceClientTypes.TestService}", 1, BindingInteraction.Response, RouteMethod.RoundRobin);
			router.AddBinding(binding);
		}
	}
}