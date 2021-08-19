using System;
using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Transactions;
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
                    cfg.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No);
                    //   cfg.AddPersistentQueues();
                    //   cfg.UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterSaved);
                })
               .ConfigureClients(cfg => { cfg.Handlers.Add(new ClientHandler()); })
               .Build();

            //rider.LoadPersistentQueues();

            rider.Transaction.CreateContainer("TransactionName",
                                              TimeSpan.FromSeconds(30),
                                              new QueueTransactionEndpoint(rider.Queue, "CommitQueue"),
                                              new QueueTransactionEndpoint(rider.Queue, "RollbackQueue"),
                                              new QueueTransactionEndpoint(rider.Queue, "TimeoutQueue"));


            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Run(9999);
        }
    }
}