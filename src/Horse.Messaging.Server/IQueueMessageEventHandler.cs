using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server;

/// <summary>
/// Queue message event handler implementation (received, consumed vs)
/// </summary>
public interface IQueueMessageEventHandler
{
    /// <summary>
    /// Triggered when a client sends a message to the server.
    /// </summary>
    Task OnProduced(HorseQueue queue, QueueMessage message, MessagingClient sender);

    /// <summary>
    /// Triggered after sending message to a consumer.
    /// This method is called for each message and each consumer.
    /// </summary>
    Task OnConsumed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver);

    /// <summary>
    /// Triggered when a receiver sends an acknowledge message.
    /// </summary>
    Task OnAcknowledged(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success);

    /// <summary>
    /// Triggered when message is queued but no receiver found and time is up
    /// </summary>
    Task MessageTimedOut(HorseQueue queue, QueueMessage message);

    /// <summary>
    /// Triggered when message requested acknowledge but acknowledge message isn't received in time
    /// </summary>
    /// <returns></returns>
    Task OnAcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery);
}