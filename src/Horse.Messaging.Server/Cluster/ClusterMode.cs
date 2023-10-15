namespace Horse.Messaging.Server.Cluster;

/// <summary>
/// Horse Clustering Mode
/// </summary>
public enum ClusterMode
{
    /// <summary>
    /// Cluster works for reliability.
    /// One mode is choosen as main, others replica.
    /// All clients are connected to main node and queue messages are replicated.
    /// If main is down, another main node is choosen.
    /// </summary>
    Reliable,

    /// <summary>
    /// Cluster works for horizontal scaling.
    /// Clients are connected to different nodes in cluster.
    /// Queue messages are kept in only one node.
    /// Other messaging operations are distributed among all nodes. 
    /// </summary>
    Scaled
}