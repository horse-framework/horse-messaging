using Horse.Messaging.Server.Cluster;

namespace HorseService;

public class NodeOptions
{
    public string Name { get; set; }
    public string Host { get; set; }
}

public class AppOptions
{
    public string NodeName { get; set; }
    public string Host { get; set; }
    public List<NodeOptions> OtherNodes { get; } = new List<NodeOptions>();

    public string ClusterSecret { get; set; }

    public int Port { get; set; } = 2626;
    public int JockeyPort { get; set; } = 2627;

    public bool JockeyEnabled { get; set; }
    public string JockeyUsername { get; set; } = "";
    public string JockeyPassword { get; set; } = "";

    public string DataPath { get; set; } = "/etc/horse/data";

    public ClusterMode ClusterMode { get; set; }
    public ReplicaAcknowledge ReplicaAcknowledge { get; set; }

    public static AppOptions LoadFromEnvironment()
    {
        AppOptions options = new AppOptions();

        string datapath = Environment.GetEnvironmentVariable("HORSE_DATA_PATH");
        if (!string.IsNullOrEmpty(datapath))
            options.DataPath = datapath;

        if (!Directory.Exists(options.DataPath))
            Directory.CreateDirectory(options.DataPath);
        
        options.JockeyEnabled = Environment.GetEnvironmentVariable("HORSE_JOCKEY") == "1";
        options.JockeyUsername = Environment.GetEnvironmentVariable("HORSE_JOCKEY_USERNAME") ?? "";
        options.JockeyPassword = Environment.GetEnvironmentVariable("HORSE_JOCKEY_PASSWORD") ?? "";

        options.NodeName = Environment.GetEnvironmentVariable("HORSE_NODE_NAME") ?? "Server";
        options.Host = Environment.GetEnvironmentVariable("HORSE_HOST") ?? "localhost";
        options.ClusterSecret = Environment.GetEnvironmentVariable("HORSE_CLUSTER_SECRET") ?? "no-secret";

        for (int i = 1; i < 11; i++)
        {
            string otherNode = Environment.GetEnvironmentVariable($"HORSE_NODE{i}");

            if (string.IsNullOrEmpty(otherNode))
                break;

            string[] values = otherNode.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 2)
                continue;

            options.OtherNodes.Add(new NodeOptions {Name = values[0], Host = values[1]});
        }

        string clusterType = Environment.GetEnvironmentVariable("HORSE_CLUSTER_TYPE");
        options.ClusterMode = !string.IsNullOrEmpty(clusterType) && clusterType.Equals("Reliable", StringComparison.InvariantCultureIgnoreCase)
            ? ClusterMode.Reliable
            : ClusterMode.Scaled;

        string replicaAck = Environment.GetEnvironmentVariable("HORSE_REPLICA_ACK");

        if (string.IsNullOrEmpty(replicaAck) || replicaAck.Equals("none", StringComparison.InvariantCultureIgnoreCase))
            options.ReplicaAcknowledge = ReplicaAcknowledge.None;
        else
            options.ReplicaAcknowledge = replicaAck.Equals("all", StringComparison.InvariantCultureIgnoreCase)
                ? ReplicaAcknowledge.AllNodes
                : ReplicaAcknowledge.OnlySuccessor;

        return options;
    }
}