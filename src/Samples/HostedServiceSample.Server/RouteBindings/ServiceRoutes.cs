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
			IRouter directRouter = rider.Router.Add("test-router", RouteMethod.Distribute);
			SampleDirectBinding testDirectBinding = new("test-binding", $"@type:test-consumer", 1, BindingInteraction.Response, RouteMethod.RoundRobin);
			directRouter.AddBinding(testDirectBinding);

			IRouter queueRouter = rider.Router.Add("test-queue-router", RouteMethod.Distribute);
			QueueBinding testQueueBinding = new("test-queue-binding", "TestQueueModel2", 1, BindingInteraction.Response);
			queueRouter.AddBinding(testQueueBinding);
		}
	}
}