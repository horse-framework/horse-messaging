using System.Collections.Generic;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Horse Messaging Cluster Options
    /// </summary>
    public class ClusterOptions
    {
        /// <summary>
        /// Cluster mode
        /// </summary>
        public ClusterMode Mode { get; set; }

        /// <summary>
        /// Waiting acknowledge option while sending the queue message to replicated queues on other nodes
        /// </summary>
        public ReplicaAcknowledge Acknowledge { get; set; }

        /// <summary>
        /// Node name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Node internal host name such as horse://localhost:12345
        /// </summary>
        public string NodeHost { get; set; }

        /// <summary>
        /// Node public host. Clients are connected to the node with that endpoint.
        /// </summary>
        public string PublicHost { get; set; }

        /// <summary>
        /// Shared secret between nodes to authenticate them each other.
        /// </summary>
        public string SharedSecret { get; set; }

        /// <summary>
        /// Other node definitions in the cluster.
        /// </summary>
        public List<NodeInfo> Nodes { get; } = new();
    }
}