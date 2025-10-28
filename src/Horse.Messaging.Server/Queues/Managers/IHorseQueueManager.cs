using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Server.Queues.Managers;

/// <summary>
/// Queue manager implementation.
/// That object manages message stores, queue sync operations and message delivery decisions.
/// </summary>
public interface IHorseQueueManager
{
    /// <summary>
    /// The queue that is being managed
    /// </summary>
    HorseQueue Queue { get; }

    /// <summary>
    /// Message store for regular messages
    /// </summary>
    IQueueMessageStore MessageStore { get; }

    /// <summary>
    /// Message store for high priority messages
    /// </summary>
    IQueueMessageStore PriorityMessageStore { get; }

    /// <summary>
    /// Delivery handler of the queue
    /// </summary>
    IQueueDeliveryHandler DeliveryHandler { get; }

    /// <summary>
    /// Reliable clustering synchronizer
    /// </summary>
    IQueueSynchronizer Synchronizer { get; }

    /// <summary>
    /// That method is called when the queue is being initialized.
    /// </summary>
    Task Initialize();

    /// <summary>
    /// That method is called when the queue is destroyed
    /// </summary>
    Task Destroy();

    /// <summary>
    /// That method is called when a message is timed out
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    Task OnMessageTimeout(QueueMessage message);

    /// <summary>
    /// Adds a message into the related message store
    /// </summary>
    bool AddMessage(QueueMessage message);

    /// <summary>
    /// Removes a message from the store
    /// </summary>
    Task<bool> RemoveMessage(QueueMessage message);

    /// <summary>
    /// Removes a message from the store
    /// </summary>
    Task<bool> RemoveMessage(string messageId);

    /// <summary>
    /// Saves the message and returns true if the message is saved.
    /// That method should return false for non persistent managers.
    /// </summary>
    Task<bool> SaveMessage(QueueMessage message);

    /// <summary>
    /// Changes message priority
    /// </summary>
    Task<bool> ChangeMessagePriority(QueueMessage message, bool priority);
}