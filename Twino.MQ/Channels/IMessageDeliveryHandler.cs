using System.Threading.Tasks;

namespace Twino.MQ.Channels
{
    public enum DeliveryDecision
    {
        Allow,
        AllowAll,
        Skip,
        SkipAll
    }

    public enum DeliveryOperation
    {
        Nothing,
        SaveMessage,
        RemoveMessage,
        SaveAndRemoveMessage
    }

    public interface IMessageDeliveryHandler
    {
        /// <summary>
        /// Before send the message.
        /// When this method is called, message isn't sent to anyone
        /// </summary>
        Task<DeliveryDecision> OnSendStarting();
        
        /// <summary>
        /// Before sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// This method decides if it is sent.
        /// </summary>
        Task<DeliveryDecision> OnBeforeSend();
        
        /// <summary>
        /// After sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// </summary>
        Task<DeliveryOperation> OnAfterSend();
        
        /// <summary>
        /// Called when a message sending operation is completed.
        /// </summary>
        Task<DeliveryOperation> OnSendCompleted();

        /// <summary>
        /// Called when a receiver sends a delivery message.
        /// </summary>
        Task<DeliveryOperation> OnDelivery();

        /// <summary>
        /// Called when a receiver sends a response message.
        /// </summary>
        Task<DeliveryOperation> OnResponse();
        
        /// <summary>
        /// Message is queued but no receiver found and time is up
        /// </summary>
        Task OnTimeUp();
        
        /// <summary>
        /// Message is about to remove
        /// </summary>
        Task OnRemove();

        /// <summary>
        /// Called when message requested delivery but delivery message isn't received in time
        /// </summary>
        /// <returns></returns>
        Task OnDeliveryTimeUp();
    }
}