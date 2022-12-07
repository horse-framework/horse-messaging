using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Routing;
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

                    /* cfg.UseMemoryQueues(c =>
                    {
                        c.Options.CommitWhen = CommitWhen.AfterReceived;
                        c.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                        c.Options.PutBack = PutBackDecision.Regular;
                    }); */
                    
                    cfg.UsePersistentQueues(null, c =>
                    {
                        c.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                        c.Options.CommitWhen = CommitWhen.AfterReceived;
                    });
                })
                .ConfigureClients(cfg => { cfg.Handlers.Add(new ClientHandler()); })
                .Build();

            IRouter router = rider.Router.Find("test");
            /*
            IRouter router = rider.Router.Add("test", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding
            {
                Name = "binding1",
                Target = "queue1",
                Interaction = BindingInteraction.Response,
                Priority = 400,
                RouteMethod = RouteMethod.Distribute
            });*/
            
            /*

            rider.Transaction.CreateContainer("TransactionName",
                TimeSpan.FromSeconds(30),
                new QueueTransactionEndpoint(rider.Queue, "CommitQueue"),
                new QueueTransactionEndpoint(rider.Queue, "RollbackQueue"),
                new QueueTransactionEndpoint(rider.Queue, "TimeoutQueue"));
*/
            HorseServer server = new HorseServer();
            server.Options.PingInterval = 10;
            server.UseRider(rider);
            server.Run(26222);
        }
    }
}