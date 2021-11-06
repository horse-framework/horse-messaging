namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Node states
    /// </summary>
    public enum NodeState
    {
        /// <summary>
        /// Cluster has only one active node or cluster mode is Scaled.
        /// If there is only one node clustering operations are disabled.
        /// If cluster mode is scaled each node works like single node
        /// But sends messaging operations (except queues) to all other nodes. 
        /// </summary>
        Single = 0,
        
        /// <summary>
        /// The node is main. All clients are connected to that node.
        /// Queue messages are replicated to other nodes.
        /// </summary>
        Main = 1,
        
        /// <summary>
        /// The node is Replica but it's the next main node if current main is down
        /// </summary>
        Successor = 2,
        
        /// <summary>
        /// The node is Replica. There is no clients connected to it.
        /// Main node sends replication data to that node.
        /// </summary>
        Replica = 3
    }
}