using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Message send and receive operations implementation
    /// (before starting to send, before and after single message sending, after sending completed, delivery and responses, time up)
    /// </summary>
    public interface IMessageDeliveryHandler
    {
        /// <summary>
        /// When a client sends a message to the server.
        /// </summary>
        Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender);

        /// <summary>
        /// Before send the message.
        /// When this method is called, message isn't sent to anyone.
        /// </summary>
        Task<Decision> BeginSend(HorseQueue queue, QueueMessage message);

        /// <summary>
        /// Before sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// This method decides if it is sent.
        /// </summary>
        Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver);

        /// <summary>
        /// After sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// </summary>
        Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver);

        /// <summary>
        /// After sending message to a receiver.
        /// This method is called for each message and each receiver.
        /// </summary>
        Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver);

        /// <summary>
        /// Called when a message sending operation is completed.
        /// </summary>
        Task<Decision> EndSend(HorseQueue queue, QueueMessage message);

        /// <summary>
        /// Called when a receiver sends an acknowledge message.
        /// </summary>
        Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success);

        /// <summary>
        /// Message is queued but no receiver found and time is up
        /// </summary>
        Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message);

        /// <summary>
        /// Called when message requested acknowledge but acknowledge message isn't received in time
        /// </summary>
        /// <returns></returns>
        Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery);

        /// <summary>
        /// Message is dequeued
        /// </summary>
        Task MessageDequeued(HorseQueue queue, QueueMessage message);

        /// <summary>
        /// Called when an exception is thrown
        /// </summary>
        Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception);

        /// <summary>
        /// After the operation of the message is completed, if save is selected, this method is called.
        /// Returns true if save operation is successful.
        /// After save operation, OnRemove will be called.
        /// You can check if IsSaved true or not. 
        /// </summary>
        Task<bool> SaveMessage(HorseQueue queue, QueueMessage message);
    }
}