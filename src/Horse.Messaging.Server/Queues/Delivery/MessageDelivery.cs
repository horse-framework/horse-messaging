using System;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Queues.Delivery;

/// <summary>
/// Delivery acknowledge status
/// </summary>
public enum DeliveryAcknowledge
{
    /// <summary>
    /// There is no ack message from consumer
    /// </summary>
    None,

    /// <summary>
    /// Consumer sent a successful acknowledge for the message
    /// </summary>
    Acknowledge,

    /// <summary>
    /// Consumer sent a negative acknowledge for the message
    /// </summary>
    NegativeAcknowledge,

    /// <summary>
    /// Acknowledge timed out
    /// </summary>
    Timeout
}

/// <summary>
/// Message delivery data for a single message to a single receiver
/// </summary>
public class MessageDelivery
{
    #region Properties

    /// <summary>
    /// The message
    /// </summary>
    public QueueMessage Message { get; }

    /// <summary>
    /// Message receiver client
    /// </summary>
    public QueueClient Receiver { get; }

    /// <summary>
    /// If true, message is sent
    /// </summary>
    public bool IsSent { get; private set; }

    /// <summary>
    /// Message send date
    /// </summary>
    public DateTime SendDate { get; private set; }

    /// <summary>
    /// Message acknowledge status
    /// </summary>
    public DeliveryAcknowledge Acknowledge { get; private set; }

    /// <summary>
    /// Acknowledge receive time
    /// </summary>
    public DateTime AcknowledgeDate { get; private set; }

    /// <summary>
    /// Acknowledge wait deadline.
    /// If this value is set and acknowledge isn't received, time up methods will be called.
    /// </summary>
    public DateTime? AcknowledgeDeadline { get; }

    #endregion

    #region Constructurs

    /// <summary>
    /// Creates new message without acknowledge deadline
    /// </summary>
    internal MessageDelivery(QueueMessage message, QueueClient receiver)
        : this(message, receiver, DateTime.MinValue)
    {
        Message = message;
        Receiver = receiver;
    }

    /// <summary>
    /// Creates new message with acknowledge deadline
    /// </summary>
    internal MessageDelivery(QueueMessage message, QueueClient receiver, DateTime? acknowledgeDeadline)
    {
        Message = message;
        Receiver = receiver;
        AcknowledgeDeadline = acknowledgeDeadline;
    }

    #endregion

    #region Actions

    /// <summary>
    /// Marks message as sent
    /// </summary>
    public void MarkAsSent()
    {
        Message.MarkAsSent();

        IsSent = true;
        SendDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks message as acknowledged
    /// </summary>
    public void MarkAsAcknowledged(bool success)
    {
        Acknowledge = success
            ? DeliveryAcknowledge.Acknowledge
            : DeliveryAcknowledge.NegativeAcknowledge;

        AcknowledgeDate = DateTime.UtcNow;

        if (Receiver != null)
        {
            Receiver.AcknowledgeCount++;
            if (Receiver.CurrentlyProcessing == Message)
                Receiver.CurrentlyProcessing = null;
        }
    }

    /// <summary>
    /// Marks acknowledge is timed up
    /// </summary>
    public bool MarkAsAcknowledgeTimeout()
    {
        if (Receiver != null)
        {
            Receiver.AcknowledgeCount++;
            if (Receiver.CurrentlyProcessing == Message)
                Receiver.CurrentlyProcessing = null;
        }

        if (Acknowledge != DeliveryAcknowledge.None)
            return false;

        Acknowledge = DeliveryAcknowledge.Timeout;
        return true;
    }

    #endregion
}