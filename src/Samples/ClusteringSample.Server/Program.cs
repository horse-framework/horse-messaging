using System;
using ClusteringSample.Producer;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace ClusteringSample.Server
{
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
                    q.UseMemoryQueues();
                })
                .ConfigureClients(c =>
                {
                    c.Handlers.Add(new ClientEventHandler());
                })
                .Build();

            rider.Cluster.Options.Name = $"Server-{port}";
            rider.Cluster.Options.SharedSecret = "top-secret";
            rider.Cluster.Options.NodeHost = $"horse://localhost:{port}";
            rider.Cluster.Options.PublicHost = $"horse://localhost:{port}";
            rider.Cluster.Options.Acknowledge = ReplicaAcknowledge.OnlySuccessor;

            foreach (int nodePort in otherNodePorts)
            {
                rider.Cluster.Options.Nodes.Add(new NodeInfo
                {
                    Name = $"Server-{nodePort}",
                    Host = $"horse://localhost:{nodePort}",
                    PublicHost = $"horse://localhost:{nodePort}"
                });
            }

            HorseServer server = new HorseServer();
            server.Logger = new ConsoleLogger();
            server.UseRider(rider);
            server.Start(port);
        }
    }
}