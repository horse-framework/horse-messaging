using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    /// <summary>
    /// Message sending decisions
    /// </summary>
    public enum MessageDecision
    {
        /// <summary>
        /// Allow to send the message to the client
        /// </summary>
        Allow,

        /// <summary>
        /// Saves message and allows to send
        /// </summary>
        AllowAndSave,

        /// <summary>
        /// Skips the send operation of the message to the client
        /// </summary>
        Skip,

        /// <summary>
        /// Saves message and  skips the send operation of the message to the client
        /// </summary>
        SkipAndSave
    }

    /// <summary>
    /// Message sending decisions
    /// </summary>
    public enum DeliveryDecision
    {
        /// <summary>
        /// Allow to send the message to the client
        /// </summary>
        Allow,

        /// <summary>
        /// Skip the send operation of the message to the client
        /// </summary>
        Skip
    }

    /// <summary>
    /// Message delivery operations
    /// </summary>
    public enum DeliveryOperation
    {
        /// <summary>
        /// Operation completed but sending operation is skipped,
        /// Message is going to be re-added in front of the queue.
        /// </summary>
        Keep,

        /// <summary>
        /// Remove message from the list and do not save
        /// </summary>
        DontSaveMesage,

        /// <summary>
        /// Save the message
        /// </summary>
        SaveMessage
    }

    /// <summary>
    /// Message send and receive operations implementation
    /// (before starting to send, before and after single message sending, after sending completed, delivery and responses, time up)
    /// </summary>
    public interface IMessageDeliveryHandler
    {
        /// <summary>
        /// When a client sends a message to the server.
        /// </summary>
        Task<MessageDecision> OnReceived(ChannelQueue queue, QueueMessage message, MqClient sender);

        /// <summary>
        /// Before send the message.
        /// When this method is called, message isn't sent to anyone.
        /// </summary>
        Task<MessageDecision> OnSendStarting(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Before sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// This method decides if it is sent.
        /// </summary>
        Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, QueueMessage message, MqClient receiver);

        /// <summary>
        /// After sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// </summary>
        Task OnAfterSend(ChannelQueue queue, MessageDelivery delivery, MqClient receiver);

        /// <summary>
        /// Called when a message sending operation is completed.
        /// </summary>
        Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when a receiver sends an acknowledge message.
        /// </summary>
        Task OnAcknowledge(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery);

        /// <summary>
        /// Message is queued but no receiver found and time is up
        /// </summary>
        Task OnTimeUp(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when message requested acknowledge but acknowledge message isn't received in time
        /// </summary>
        /// <returns></returns>
        Task OnAcknowledgeTimeUp(ChannelQueue queue, MessageDelivery delivery);

        /// <summary>
        /// Message is about to remove
        /// </summary>
        Task OnRemove(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when an exception is thrown
        /// </summary>
        Task OnException(ChannelQueue queue, QueueMessage message, Exception exception);

        /// <summary>
        /// After the operation of the message is completed, if save is selected, this method is called.
        /// Returns true if save operation is successful.
        /// After save operation, OnRemove will be called.
        /// You can check if IsSaved true or not. 
        /// </summary>
        Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message);
    }
}