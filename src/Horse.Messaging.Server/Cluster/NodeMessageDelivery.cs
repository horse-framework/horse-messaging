using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Cluster
{
    internal class NodeMessageDelivery
    {
        public bool IsCommitted { get; set; }
        public bool IsTimedOut { get; set; }
        public DateTime Expiration { get; set; }
        public HorseMessage Message { get; set; }
        public TaskCompletionSource<NodeMessageDelivery> CompletionSource { get; set; }
    }
}