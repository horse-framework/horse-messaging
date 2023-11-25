using System;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Sample.Cluster;

public class ConsoleLogger : ILogger
{
    public void LogException(string hint, Exception exception)
    {
        Console.WriteLine("ERROR: " + hint + " - " + exception);
    }

    public void LogEvent(string hint, string message)
    {
        Console.WriteLine(hint + "\t" + message);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            StartServer2();
            Console.ReadLine();
            return;
        }

        switch (args[0].Trim())
        {
            case "1":
                StartServer1();
                break;

            case "2":
                StartServer2();
                break;

            case "3":
                StartServer3();
                break;

            default:
                Console.WriteLine("Invalid arg");
                break;
        }

        Console.ReadLine();
    }

    static HorseRider StartServer1()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q => q.UseMemoryQueues())
            .Build();

        rider.Cluster.Options.Name = "Server1";
        rider.Cluster.Options.SharedSecret = "top-secret";
        rider.Cluster.Options.NodeHost = "horse://localhost:26101";
        rider.Cluster.Options.PublicHost = "horse://localhost:26101";

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server2",
            Host = "horse://localhost:26102",
            PublicHost = "horse://localhost:26102"
        });

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server3",
            Host = "horse://localhost:26103",
            PublicHost = "horse://localhost:26103"
        });

        HorseServer server = new HorseServer();
        server.Logger = new ConsoleLogger();
        server.UseRider(rider);
        server.Start(26101);
        return rider;
    }

    static HorseRider StartServer2()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q => q.UseMemoryQueues(c => c.Options.CommitWhen = CommitWhen.AfterReceived))
            .Build();

        rider.Cluster.Options.Name = "Server2";
        rider.Cluster.Options.SharedSecret = "top-secret";
        rider.Cluster.Options.NodeHost = "horse://localhost:26102";
        rider.Cluster.Options.PublicHost = "horse://localhost:26102";

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server1",
            Host = "horse://localhost:26101",
            PublicHost = "horse://localhost:26101"
        });

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server3",
            Host = "horse://localhost:26103",
            PublicHost = "horse://localhost:26103"
        });

        HorseServer server = new HorseServer();
        server.Logger = new ConsoleLogger();
        server.UseRider(rider);
        server.Start(26102);
        return rider;
    }

    static HorseRider StartServer3()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q => q.UseMemoryQueues(c => c.Options.CommitWhen = CommitWhen.AfterReceived))
            .Build();

        rider.Cluster.Options.Name = "Server3";
        rider.Cluster.Options.SharedSecret = "top-secret";
        rider.Cluster.Options.NodeHost = "horse://localhost:26103";
        rider.Cluster.Options.PublicHost = "horse://localhost:26103";

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server1",
            Host = "horse://localhost:26101",
            PublicHost = "horse://localhost:26101"
        });

        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = "Server2",
            Host = "horse://localhost:26102",
            PublicHost = "horse://localhost:26102"
        });

        HorseServer server = new HorseServer();
        server.Logger = new ConsoleLogger();
        server.UseRider(rider);
        server.Start(26103);
        return rider;
    }
}