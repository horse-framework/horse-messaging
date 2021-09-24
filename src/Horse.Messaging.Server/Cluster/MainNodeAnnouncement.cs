namespace Horse.Messaging.Server.Cluster
{
    public class MainNodeAnnouncement
    {
        public NodeInfo Main { get; set; }
        public NodeInfo Successor { get; set; }
    }
}