
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
        
        //todo: queue options properties
        //Acknowledge =
        //AcknowledgeTimeout =
        //AutoDestroy =
        //ClientLimit =
        //MessageLimit =
        //MessageTimeout = 
        //DelayBetweenMessages =
        //MessageSizeLimit =
        //PutBackDelay = 
    }
}