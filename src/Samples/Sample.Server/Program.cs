using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Server;

namespace Sample.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            HorseRider rider = HorseRiderBuilder.Create()
               .ConfigureQueues(cfg =>
                {
                    cfg.Options.Type = QueueType.Push;
                    cfg.EventHandlers.Add(new QueueEventHandler());
                    cfg.AddPersistentQueues();
                    cfg.UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterSaved);
                })
               .ConfigureClients(cfg => { cfg.Handlers.Add(new ClientHandler()); })
               .Build();

            rider.LoadPersistentQueues();

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Run(9999);
        }
    }
}