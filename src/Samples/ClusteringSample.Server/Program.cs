using System;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Sync;
using Horse.Server;

namespace ClusteringSample.Server;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
            StartServer(26101, 26102, 26103);
        else
            StartServer(Convert.ToInt32(args[0]),
                Convert.ToInt32(args[1]),
                Convert.ToInt32(args[2]));
    }

    private static void StartServer(int port, params int[] otherNodePorts)
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.UsePersistentQueues(c =>
                {
                    c.UseInstantFlush();
                });
                //q.UseMemoryQueues();
                q.MessageHandlers.Add(new QueueMessageHandler());
            })
            .ConfigureClients(c => { c.Handlers.Add(new ClientEventHandler()); })
            .Build();

        rider.ErrorHandlers.Add(new ConsoleErrorHandler());

        rider.Cluster.Options.Name = $"Server-{port}";
        rider.Cluster.Options.SharedSecret = "top-secret";
        rider.Cluster.Options.NodeHost = $"horse://localhost:{port}";
        rider.Cluster.Options.PublicHost = $"horse://localhost:{port}";
        rider.Cluster.Options.Acknowledge = ReplicaAcknowledge.AllNodes;

        foreach (int nodePort in otherNodePorts)
        {
            rider.Cluster.Options.Nodes.Add(new NodeInfo
            {
                Name = $"Server-{nodePort}",
                Host = $"horse://localhost:{nodePort}",
                PublicHost = $"horse://localhost:{nodePort}"
            });
        }

        _ = Task.Factory.StartNew(async () =>
        {
            while (true)
                try
                {
                    await Task.Delay(5000);
                    foreach (HorseQueue queue in rider.Queue.Queues)
                    {
                        Console.WriteLine($"QUEUE {queue.Name} has {queue.Manager.MessageStore.Count()} Messages");
                        if (queue.Manager == null)
                            continue;

                        if (queue.Manager.Synchronizer.Status == QueueSyncStatus.None)
                            continue;

                        Console.WriteLine($"Queue {queue.Name} Sync Status is {queue.Manager.Synchronizer.Status}");
                    }
                }
                catch
                {
                }
        });

        HorseServer server = new HorseServer();
        server.Logger = new ConsoleLogger();
        server.UseRider(rider);
        server.Run(port);
    }
}