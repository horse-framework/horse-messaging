using System;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Benchmark.Server
{
    class Program
    {
        private static HorseRider _rider;

        static void Main(string[] args)
        {
            _rider = HorseRiderBuilder.Create()
                .ConfigureQueues(cfg =>
                {
                    cfg.EventHandlers.Add(new QueueEventHandler());
                    //cfg.Options.Acknowledge = QueueAckDecision.None;
                    cfg.Options.Type = QueueType.Push;
                    cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
                    cfg.UseMemoryQueues();
                    //cfg.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No);
                })
                .ConfigureCache(cfg => { cfg.Options.DefaultDuration = TimeSpan.FromMinutes(30); })
                .AddErrorHandler<ErrorHandler>()
                .Build();

            HorseServer server = new HorseServer();
            server.UseRider(_rider);
            server.Run(27001);
        }
    }
}