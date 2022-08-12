using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Sync
{
    /// <summary>
    /// Queue synchronizer implementation for reliable clusters
    /// </summary>
    public interface IQueueSynchronizer
    {
        /// <summary>
        /// Queue manager
        /// </summary>
        IHorseQueueManager Manager { get; }
        
        /// <summary>
        /// Synchorization status
        /// </summary>
        QueueSyncStatus Status { get; }
        
        /// <summary>
        /// Client object of the remote node 
        /// </summary>
        NodeClient RemoteNode { get; }

        /// <summary>
        /// Begins sharing the queue messages with remote node.
        /// That method is called when the node is main.
        /// </summary>
        Task<bool> BeginSharing(NodeClient replica);

        /// <summary>
        /// Begins receiving messages from main node.
        /// That method is called to get prepared the replica node for receiving messages from main.
        /// </summary>
        Task<bool> BeginReceiving(NodeClient main);

        /// <summary>
        /// This method is called for replica nodes when the main node sends id list of all messages in the queue.
        /// </summary>
        Task ProcessMessageList(HorseMessage message);

        /// <summary>
        /// This methos is called for main node right after replica is requested missing messages.
        /// </summary>
        Task SendMessages(HorseMessage requestMessage);

        /// <summary>
        /// This method is called when main node sends missing messages to the replica.
        /// </summary>
        Task ProcessReceivedMessages(HorseMessage message);
        
        /// <summary>
        /// This method is executed in main node right after replica send sync completion message to the main.
        /// It also unlocks the queue to regular queue operations.
        /// </summary>
        Task EndSharing();

        /// <summary>
        /// This method is executed in replica node.
        /// Completes queue sync operations and sends the sync completion message to the main.
        /// </summary>
        Task EndReceiving();
    }
}