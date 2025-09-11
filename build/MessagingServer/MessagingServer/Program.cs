using Horse.Jockey;
using Horse.Jockey.Models.User;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

// Load Options

string datapath = Environment.GetEnvironmentVariable("HORSE_DATA_PATH") ?? "etc/horse/data\"";
string clusterType = Environment.GetEnvironmentVariable("HORSE_CLUSTER_TYPE") ?? "scaled";
string nodeName = Environment.GetEnvironmentVariable("HORSE_NODE_NAME") ?? "Default";
string otherNodes = Environment.GetEnvironmentVariable("HORSE_NODES") ?? "";
string clusterSecret = Environment.GetEnvironmentVariable("HORSE_CLUSTER_SECRET") ?? "";
string jockey = Environment.GetEnvironmentVariable("HORSE_JOCKEY") ?? "1";
string username = Environment.GetEnvironmentVariable("HORSE_JOCKEY_USERNAME") ?? string.Empty;
string password = Environment.GetEnvironmentVariable("HORSE_JOCKEY_PASSWORD") ?? string.Empty;

// Initialize Server
HorseServer server = new HorseServer();

HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureOptions(o => { o.DataPath = datapath; })
    .ConfigureChannels(c =>
    {
        c.Options.AutoDestroy = true;
        c.Options.AutoChannelCreation = true;
    })
    .ConfigureCache(c =>
    {
        c.Options.DefaultDuration = TimeSpan.FromMinutes(15);
        c.Options.MinimumDuration = TimeSpan.FromHours(6);
    })
    .ConfigureQueues(c =>
    {
        c.Options.AutoQueueCreation = true;
        c.UsePersistentQueues(d => { d.UseAutoFlush(TimeSpan.FromMilliseconds(50)); },
            q =>
            {
                q.Options.AutoDestroy = QueueDestroy.Disabled;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.PutBack = PutBackDecision.Regular;
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                q.Options.PutBackDelay = 5000;
            });
    })
    .Build();


// Clustering options
if (!string.IsNullOrEmpty(otherNodes))
{
    rider.Cluster.Options.Mode = !string.IsNullOrEmpty(clusterType) && clusterType.Equals("Reliable", StringComparison.InvariantCultureIgnoreCase)
        ? ClusterMode.Reliable
        : ClusterMode.Scaled;

    rider.Cluster.Options.Name = nodeName;
    rider.Cluster.Options.NodeHost = "horse://localhost:2628";
    rider.Cluster.Options.PublicHost = $"horse://localhost:2626";
    rider.Cluster.Options.SharedSecret = clusterSecret;

    foreach (string otherNode in otherNodes.Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        rider.Cluster.Options.Nodes.Add(new NodeInfo
        {
            Name = otherNode,
            Host = $"horse://{otherNode}:2628",
            PublicHost = $"horse://{otherNode}:2626"
        });
    }
}

// Add Jockey
bool skipJockey = !string.IsNullOrEmpty(jockey) && jockey == "0";
if (!skipJockey)
{
    rider.AddJockey(o =>
    {
        o.CustomSecret = $"{Guid.NewGuid()}-{Guid.NewGuid()}-{Guid.NewGuid()}";

        o.Port = 2627;
        o.AuthAsync = login =>
        {
            if (login.Username == username && login.Password == password)
                return Task.FromResult(new UserInfo { Id = "*", Name = "Admin" });

            return Task.FromResult<UserInfo>(null);
        };
    });
}


// Run
server.UseRider(rider);

server.UseHorseOverWebsockets(opt => { opt.Port = 2680; });
server.Options.Hosts.Add(new HostOptions { Port = 2626 });

server.Run();