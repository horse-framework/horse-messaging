using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Routing;
using HostedServiceSample.Server.CustomBindings;

namespace AdvancedSample.Messaging.Server.RouteBindings
{
	public static class ServiceRoutes
	{
		public static void ConfigureServiceRoutes(this HorseRider rider)
		{
			IRouter router = rider.Router.Add("test-router", RouteMethod.Distribute);
			SampleDirectBinding binding = new("test-binding", $"@type:test-consumer", 1, BindingInteraction.Response, RouteMethod.RoundRobin);
			router.AddBinding(binding);
		}
	}
}