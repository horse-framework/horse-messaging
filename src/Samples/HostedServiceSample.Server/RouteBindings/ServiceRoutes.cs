using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Routing;
using HostedServiceSample.Server.CustomBindings;

namespace HostedServiceSample.Server.RouteBindings
{
    public static class ServiceRoutes
    {
        public static void ConfigureServiceRoutes(this HorseRider rider)
        {
            Router directRouter = rider.Router.Add("test-router", RouteMethod.Distribute);
            SampleDirectBinding testDirectBinding = new SampleDirectBinding
            {
                Name = "test-binding",
                Target = $"@type:test-consumer",
                Priority = 1,
                Interaction = BindingInteraction.Response,
                RouteMethod = RouteMethod.RoundRobin
            };
            directRouter.AddBinding(testDirectBinding);

            Router queueRouter = rider.Router.Add("test-queue-router", RouteMethod.Distribute);
            QueueBinding testQueueBinding = new QueueBinding
            {
                Name = "test-queue-binding",
                Target = "TestQueueModel2",
                Priority = 1,
                Interaction = BindingInteraction.Response
            };
            queueRouter.AddBinding(testQueueBinding);
        }
    }
}