using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;

namespace Horse.Messaging.Server.Queues.Sync
{
    public enum QueueSyncStatus
    {
        None,
        Sharing,
        Receiving
    }
    
    public interface IQueueSynchronizer
    {
        IHorseQueueManager Manager { get; }
        QueueSyncStatus Status { get; }
        NodeClient RemoteNode { get; }

        Task<bool> BeginSharing(NodeClient replica);

        Task<bool> BeginReceiving(NodeClient main);

        Task ProcessMessageList(HorseMessage message);

        Task SendMessages(HorseMessage requestMessage);

        Task ProcessReceivedMessages(HorseMessage message);
        
        Task EndSharing();

        Task EndReceiving();
    }
}