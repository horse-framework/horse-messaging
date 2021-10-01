using System.Collections.Generic;

namespace Horse.Messaging.Server.Cluster
{
    public class NodeQueueHandlerHeader
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class NodeQueueInfo
    {
        public string Name { get; set; }
        public string HandlerName { get; set; }

        public string QueueType { get; set; }

        public bool Initialized { get; set; }

        public NodeQueueHandlerHeader[] Headers { get; internal set; }

        public string Topic { get; set; }
        public string Acknowledge { get; set; }

        public int AcknowledgeTimeout { get; set; }

        public string AutoDestroy { get; set; }

        public int ClientLimit { get; set; }
        public int MessageLimit { get; set; }
        public int MessageTimeout { get; set; }
        public int DelayBetweenMessages { get; set; }
        public ulong MessageSizeLimit { get; set; }
        public int PutBackDelay { get; set; }
    }
}