using System.Threading.Tasks;
using Horse.Jockey;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Server;

[QueueName("demo-queue")]
[QueueType(MessagingQueueType.Push)]
public class Model
{
    public string Foo { get; set; }
}

public class ModelConsumer : IQueueConsumer<Model>
{
    public Task Consume(HorseMessage message, Model model, HorseClient client)
    {
        throw new System.NotImplementedException();
    }
}

namespace Sample.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureQueues(cfg =>
                {
                    cfg.Options.Type = QueueType.Push;
                    cfg.Options.MessageLimit = 10;
                    cfg.Options.AutoQueueCreation = true;
                    cfg.UseMemoryQueues();
                })
                .Build();
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
            rider.AddJockey(p => p.Port = 2627);
            HorseServer server = new HorseServer();
            server.Options.PingInterval = 10;
            server.UseRider(rider);
            server.Run(2626);
        }
    }
}