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
            Router router = rider.Router.Add(AdvancedSampleServiceRoutes.TestService, RouteMethod.Distribute);
            DirectBinding binding = new DirectBinding
            {
                Name = "test-binding",
                Target = $"@type:{AdvancedSampleServiceClientTypes.TestService}",
                Priority = 1,
                Interaction = BindingInteraction.Response,
                RouteMethod = RouteMethod.RoundRobin
            };
            router.AddBinding(binding);
        }
    }
}