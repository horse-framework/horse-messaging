using System.Threading.Tasks;

namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Server-side lifecycle hook for partition events.
/// Register via HorseRider.Queue.PartitionEventHandlers.Add(...)
/// </summary>
public interface IPartitionEventHandler
{
    /// <summary>Called when a new partition is created for a partitioned queue.</summary>
    Task OnPartitionCreated(HorseQueue parentQueue, PartitionEntry partition);

    /// <summary>Called when a partition queue is destroyed.</summary>
    Task OnPartitionDestroyed(HorseQueue parentQueue, string partitionId);
}
