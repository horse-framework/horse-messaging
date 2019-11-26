using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Queue messaging is running. Messages are accepted and sent to queueus.
        /// </summary>
        Running,

        /// <summary>
        /// Queue messages are accepted and queued but not pending
        /// </summary>
        Paused,

        /// <summary>
        /// Queue messages are not accepted.
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Channel queue.
    /// Keeps queued messages and subscribed clients.
    /// </summary>
    public class ChannelQueue
    {
        #region Properties

        /// <summary>
        /// Channel of the queue
        /// </summary>
        public Channel Channel { get; }

        /// <summary>
        /// Queue status
        /// </summary>
        public QueueStatus Status { get; private set; }

        /// <summary>
        /// Queue content type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Queue options.
        /// If null, channel default options will be used
        /// </summary>
        public ChannelQueueOptions Options { get; }

        /// <summary>
        /// Queue event handler.
        /// If not null, all methods will be called when events are occured.
        /// If null, server's default event handler will be used.
        /// </summary>
        public IQueueEventHandler EventHandler { get; }

        /// <summary>
        /// Queue messaging handler.
        /// If null, server's default delivery will be used.
        /// </summary>
        public IMessageDeliveryHandler DeliveryHandler { get; }

        private readonly SafeList<QueueMessage> _prefentialMessages;
        private readonly SafeList<QueueMessage> _standardMessages;

        /// <summary>
        /// Standard prefential messages
        /// </summary>
        public IEnumerable<QueueMessage> PrefentialMessages => _prefentialMessages.GetUnsafeList();

        /// <summary>
        /// Standard queued messages
        /// </summary>
        public IEnumerable<QueueMessage> StandardMessages => _standardMessages.GetUnsafeList();

        #endregion

        #region Constructors

        internal ChannelQueue(Channel channel,
                              ushort contentType,
                              ChannelQueueOptions options,
                              IQueueEventHandler eventHandler,
                              IMessageDeliveryHandler deliveryHandler)
        {
            Channel = channel;
            ContentType = contentType;
            Options = options;

            EventHandler = eventHandler;
            DeliveryHandler = deliveryHandler;

            _prefentialMessages = new SafeList<QueueMessage>(options.PrefentialQueueCapacity);
            _standardMessages = new SafeList<QueueMessage>(options.StandardQueueCapacity);
        }

        #endregion

        #region Status Actions

        /// <summary>
        /// Sets status of the queue
        /// </summary>
        public async Task SetStatus(QueueStatus status)
        {
            QueueStatus old = Status;
            if (old == status)
                return;

            if (EventHandler != null)
            {
                bool allowed = await EventHandler.OnStatusChanged(this, old, status);
                if (!allowed)
                    return;
            }

            Status = status;
        }

        #endregion

        #region Messaging Actions

        /// <summary>
        /// Adds a new message into the queue
        /// </summary>
        public async Task AddMessage(QueueMessage message)
        {
            if (EventHandler != null)
                await EventHandler.OnMessageReceived(this, message);

            await Trigger(message);
        }

        /// <summary>
        /// Removes the message from the queue.
        /// Remove operation will be canceled If force is false and message is not sent
        /// </summary>
        public async Task<bool> RemoveMessage(QueueMessage message, bool force = false)
        {
            if (!force && !message.IsSent())
                return false;

            if (message.Message.HighPriority)
                _prefentialMessages.Remove(message);
            else
                _standardMessages.Remove(message);

            if (EventHandler != null)
                await EventHandler.OnMessageRemoved(this, message);

            return true;
        }

        /// <summary>
        /// Checks all pending messages and subscribed receivers.
        /// If they should receive the messages, runs the process. 
        /// Should be called when a new message is added.
        /// </summary>
        internal async Task Trigger(QueueMessage message)
        {
            if (Options.MessageQueuing)
            {
                if (message.Message.HighPriority)
                    _prefentialMessages.Add(message);
                else
                    _standardMessages.Add(message);
            }

            await ProcesssMessage(message);
        }

        /// <summary>
        /// Checks all pending messages and subscribed receivers.
        /// If they should receive the messages, runs the process. 
        /// Should be called when a new client is subscribed to the channel.
        /// </summary>
        internal async Task Trigger(ChannelClient subscribedClient)
        {
            if (_prefentialMessages.Count > 0)
            {
            }

            if (_standardMessages.Count > 0)
            {
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Searches receivers of the message and process the send operation
        /// </summary>
        private async Task ProcesssMessage(QueueMessage message)
        {
            DeliveryDecision decision = await DeliveryHandler.OnSendStarting(this, message);
            if (decision == DeliveryDecision.Skip)
            {
                DeliveryOperation skipOperation = await DeliveryHandler.OnSendCompleted(this, message);
                //todo: skip operation decision
                
                if (Options.MessageQueuing)
                {
                    if (message.Message.HighPriority)
                        _prefentialMessages.Remove(message);
                    else
                        _standardMessages.Remove(message);
                }

                await DeliveryHandler.OnRemove(this, message);
                return;
            }

            List<ChannelClient> clients = Channel.ClientsClone;
            if (clients.Count == 0)
            {
                //there is no receiver
                //todo: do something useful
                return;
            }

            byte[] messageData; //todo: create first acquirer data

            foreach (ChannelClient client in clients)
            {
                if (!client.Client.IsConnected)
                    continue;

                DeliveryDecision beforeSend = await DeliveryHandler.OnBeforeSend(this, message, client.Client);
                if (beforeSend == DeliveryDecision.Skip)
                {
                    //todo: do cleaning
                    return;
                }

                //todo: delivery deadline?
                MessageDelivery delivery = new MessageDelivery(message, client);

                //todo: send

                bool firstAcquirer = message.Message.FirstAcquirer;
                message.AddSend(delivery);

                if (firstAcquirer && clients.Count > 1)
                {
                    messageData = null; //todo: set new data for next acquirers
                }

                DeliveryOperation afterSend = await DeliveryHandler.OnAfterSend(this, delivery, client.Client);
            }

            DeliveryOperation sendCompleted = await DeliveryHandler.OnSendCompleted(this, message);

            //todo: do something useful with send completion
        }

        #endregion
    }
}