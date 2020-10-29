using System.Threading.Tasks;
using Sample.Server;
using Twino.MQ;
using Twino.MQ.Routing;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace RoutingSample.Server
{
	internal class Program
	{
		private static Task Main(string[] args)
		{
			TwinoMQ mq = TwinoMqBuilder.Create()
									   .AddClientHandler<ClientHandler>()
									   .AddQueueEventHandler<QueueEventHandler>()
									   .UseJustAllowDeliveryHandler()
									   .Build();

			var sampleMessageRouter = mq.AddRouter("SAMPLE-MESSAGE-ROUTER", RouteMethod.Distribute);
			var sampleMessageQueueBinding = new QueueBinding("sample-message-queue-binding", "SAMPLE-MESSAGE-QUEUE", 1, BindingInteraction.Response);
			var sampleMessageDirectBinding = new DirectBinding("sample-message-direct-binding", "@type:SAMPLE-MESSAGE-CONSUMER", 2, BindingInteraction.None, RouteMethod.RoundRobin);
			sampleMessageRouter.AddBinding(sampleMessageQueueBinding);
			sampleMessageRouter.AddBinding(sampleMessageDirectBinding);

			TwinoServer server = new TwinoServer();
			server.UseTwinoMQ(mq);
			server.Start(15500);
			return server.BlockWhileRunningAsync();
		}
	}
}