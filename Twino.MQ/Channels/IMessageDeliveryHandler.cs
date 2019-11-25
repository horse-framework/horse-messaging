using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Channels
{
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
        /// Allow to send the message to all clients
        /// </summary>
        AllowAll,
        
        /// <summary>
        /// Skip the send operation of the message to the client
        /// </summary>
        Skip,
        
        /// <summary>
        /// Skip the send operation of the message to all clients
        /// </summary>
        SkipAll
    }

    /// <summary>
    /// Message delivery operations
    /// </summary>
    public enum DeliveryOperation
    {
        /// <summary>
        /// Do nothing to keep going on
        /// </summary>
        Nothing,
        
        /// <summary>
        /// Save the message
        /// </summary>
        SaveMessage,
        
        /// <summary>
        /// Remove the message
        /// </summary>
        RemoveMessage,
        
        /// <summary>
        /// Save and remove the message
        /// </summary>
        SaveAndRemoveMessage
    }

    /// <summary>
    /// Message send and receive operations implementation
    /// (before starting to send, before and after single message sending, after sending completed, delivery and responses, time up)
    /// </summary>
    public interface IMessageDeliveryHandler
    {
        /// <summary>
        /// Before send the message.
        /// When this method is called, message isn't sent to anyone
        /// </summary>
        Task<DeliveryDecision> OnSendStarting(ChannelQueue queue, QueueMessage message);
        
        /// <summary>
        /// Before sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// This method decides if it is sent.
        /// </summary>
        Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, TmqMessage message, MqClient receiver);
        
        /// <summary>
        /// After sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// </summary>
        Task<DeliveryOperation> OnAfterSend(ChannelQueue queue, MessageDelivery delivery);
        
        /// <summary>
        /// Called when a message sending operation is completed.
        /// </summary>
        Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when a receiver sends a delivery message.
        /// </summary>
        Task<DeliveryOperation> OnDelivery(ChannelQueue queue, MessageDelivery delivery);

        /// <summary>
        /// Called when a receiver sends a response message.
        /// </summary>
        Task<DeliveryOperation> OnResponse(ChannelQueue queue, MessageDelivery delivery, TmqMessage response);
        
        /// <summary>
        /// Message is queued but no receiver found and time is up
        /// </summary>
        Task OnTimeUp(ChannelQueue queue, QueueMessage message);
        
        /// <summary>
        /// Message is about to remove
        /// </summary>
        Task OnRemove(ChannelQueue queue, QueueMessage delivery);

        /// <summary>
        /// Called when message requested delivery but delivery message isn't received in time
        /// </summary>
        /// <returns></returns>
        Task OnDeliveryTimeUp(ChannelQueue queue, MessageDelivery delivery);
    }
}