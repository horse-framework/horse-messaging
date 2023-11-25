using Horse.Messaging.Server.Cluster;

namespace AdvancedSample.Server.Models;

public class ClusterConfig
{
    public bool Enabled { get; set; }
    public string Name { get; set; }
    public ClusterMode Mode { get; set; }
    public ReplicaAcknowledge Acknowledge { get; set; }
    public string NodeHost { get; set; }
    public string PublicHost { get; set; }
    public string SharedSecret { get; set; }

    public List<ClusterNodeConfig> Nodes { get; set; }
}

public class ClusterNodeConfig
{
    public string Name { get; set; }
    public string Host { get; set; }
    public string PublicHost { get; set; }
}