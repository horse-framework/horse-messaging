using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Data;
using Twino.MQ.Events;
using Twino.MQ.Routing;
using Twino.Server;

namespace Sample.Route.Server
{
	class ClientHandler: IClientHandler
	{
		public Task Connected(TwinoMQ server, MqClient client)
		{
			Console.WriteLine("CONNECTED => " + client.Type);
			return Task.CompletedTask;
		}

		public Task Disconnected(TwinoMQ server, MqClient client)
		{
			Console.WriteLine("DISCONNECTED => " + client.Type);
			return Task.CompletedTask;
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			TwinoServer server = new TwinoServer();
			TwinoMQ mq = server.UseTwinoMQ(cfg =>
			{
				cfg.AddPersistentQueues(q => q.UseAutoFlush().KeepLastBackup());
				cfg.UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived);
				cfg.UseClientHandler(new ClientHandler());
			});
			mq.LoadPersistentQueues().GetAwaiter().GetResult();

			var sampleARouter = mq.AddRouter("sample-a-router", RouteMethod.Distribute);
			var sampleABinding = new DirectBinding("sample-a-binding", "@type:sample-a-consumer", 1, BindingInteraction.Response);
			sampleARouter.AddBinding(sampleABinding);

			var sampleBRouter = mq.AddRouter("sample-b-router", RouteMethod.Distribute);
			var sampleBBinding = new DirectBinding("sample-b-binding", "@type:sample-b-consumer", 1, BindingInteraction.Response);
			sampleBRouter.AddBinding(sampleBBinding);

			server.Start(22201);
			server.BlockWhileRunning();
		}
	}
}