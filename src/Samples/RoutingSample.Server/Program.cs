using Sample.Server;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Protocol;
using Horse.Messaging.Server.Routing;
using Horse.Server;
using QueueEventHandler = Sample.Server.QueueEventHandler;

namespace RoutingSample.Server
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			HorseMq mq = HorseMqBuilder.Create()
									   .AddClientHandler<ClientHandler>()
									   .AddQueueEventHandler<QueueEventHandler>()
									   .UseJustAllowDeliveryHandler()
									   .Build();

			var sampleMessageRouter = mq.AddRouter("SAMPLE-MESSAGE-ROUTER", RouteMethod.Distribute);
			var sampleMessageQueueBinding = new QueueBinding("sample-message-queue-binding", "SAMPLE-MESSAGE-QUEUE", 1, BindingInteraction.Response);
			var sampleMessageDirectBinding = new DirectBinding("sample-message-direct-binding", "@type:SAMPLE-MESSAGE-CONSUMER", 2, BindingInteraction.Response, RouteMethod.RoundRobin);
			sampleMessageRouter.AddBinding(sampleMessageQueueBinding);
			sampleMessageRouter.AddBinding(sampleMessageDirectBinding);

			var giveMeGuidRequestRouter = mq.AddRouter("GIVE-ME-REQUEST-ROUTER", RouteMethod.Distribute);
			var giveMeGuidRequestHandler = new DirectBinding("sample-message-direct-binding", "@name:GIVE-ME-GUID-REQUEST-HANDLER-CONSUMER", 2, BindingInteraction.Response);
			giveMeGuidRequestRouter.AddBinding(giveMeGuidRequestHandler);

			HorseServer server = new HorseServer();
			server.UseHorseMq(mq);
			server.Run(15500);
		}
	}
}