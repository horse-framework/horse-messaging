using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Queues;
using Twino.MQ.Routing;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            server.Options.PingInterval = 180;
            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
                                                  .UseClientHandler<ClientHandler>()
                                                  .UseChannelEventHandler<QueueHandler>()
                                                  .AddPersistentQueues(q => q.UseAutoFlush().KeepLastBackup())
                                                  .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.None));

            var router = mq.AddRouter("deneme-router", RouteMethod.Distribute);
            var binding = new DirectBinding("deneme-binding", "@name:consumer", 1, BindingInteraction.Response);
            router.AddBinding(binding);

            await mq.LoadPersistentQueues();

            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
}