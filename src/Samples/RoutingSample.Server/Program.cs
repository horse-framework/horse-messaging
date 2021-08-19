using Horse.Messaging.Protocol;
using Sample.Server;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Routing;
using Horse.Server;
using QueueEventHandler = Sample.Server.QueueEventHandler;

namespace RoutingSample.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            HorseRider rider = HorseRiderBuilder.Create()
               .ConfigureClients(c => c.Handlers.Add(new ClientHandler()))
               .ConfigureQueues(q => q.UseJustAllowDeliveryHandler().EventHandlers.Add(new QueueEventHandler()))
               .Build();

            var sampleMessageRouter = rider.Router.Add("SAMPLE-MESSAGE-ROUTER", RouteMethod.Distribute);
            var sampleMessageQueueBinding = new QueueBinding("sample-message-queue-binding", "SAMPLE-MESSAGE-QUEUE", 1, BindingInteraction.Response);
            var sampleMessageDirectBinding = new DirectBinding("sample-message-direct-binding", "@type:SAMPLE-MESSAGE-CONSUMER", 2, BindingInteraction.Response, RouteMethod.RoundRobin);
            sampleMessageRouter.AddBinding(sampleMessageQueueBinding);
            sampleMessageRouter.AddBinding(sampleMessageDirectBinding);

            var giveMeGuidRequestRouter = rider.Router.Add("GIVE-ME-REQUEST-ROUTER", RouteMethod.Distribute);
            var giveMeGuidRequestHandler = new DirectBinding("sample-message-direct-binding", "@name:GIVE-ME-GUID-REQUEST-HANDLER-CONSUMER", 2, BindingInteraction.Response);
            giveMeGuidRequestRouter.AddBinding(giveMeGuidRequestHandler);

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Run(15500);
        }
    }
}