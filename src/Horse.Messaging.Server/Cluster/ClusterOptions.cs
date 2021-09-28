using System.Collections.Generic;

namespace Horse.Messaging.Server.Cluster
{
    public enum ClusterMode
    {
        Single,
        Reilable,
        Scaled
    }

    public class ClusterOptions
    {
        public ClusterMode Mode { get; set; }
        public string Name { get; set; }
        public string NodeHost { get; set; }
        public string PublicHost { get; set; }
        public string SharedSecret { get; set; }

        public List<NodeInfo> Nodes { get; } = new();
    }
}