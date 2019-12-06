using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Exception thrown handler for message reader
    /// </summary>
    public delegate void ReaderExceptionThrownHandler(TmqMessage message, Exception exception);

    /// <summary>
    /// Type based message reader for TMQ client
    /// </summary>
    public class MessageReader
    {
        /// <summary>
        /// All message subscriptions
        /// </summary>
        private readonly List<QueueSubscription> _subscriptions = new List<QueueSubscription>(16);

        /// <summary>
        /// Attached TMQ clients
        /// </summary>
        private readonly List<TmqClient> _attachedClients = new List<TmqClient>(8);

        /// <summary>
        /// TmqMessage to model type converter function
        /// </summary>
        private Func<TmqMessage, Type, object> _func;

        /// <summary>
        /// This event is triggered when user defined action throws an exception
        /// </summary>
        public event ReaderExceptionThrownHandler OnException;

        /// <summary>
        /// Creates new message reader with converter action
        /// </summary>
        public MessageReader(Func<TmqMessage, Type, object> func)
        {
            _func = func;
        }

        /// <summary>
        /// Creates new message reader, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        public static MessageReader JsonReader()
        {
            return new MessageReader((msg, type) => System.Text.Json.JsonSerializer.Deserialize(msg.ToString(), type));
        }

        /// <summary>
        /// Attach the client to the message reader and starts to read messages
        /// </summary>
        public void Attach(TmqClient client)
        {
            client.MessageReceived += ClientOnMessageReceived;

            lock (_attachedClients)
                _attachedClients.Add(client);
        }

        /// <summary>
        /// Detach the client from reading it's messages
        /// </summary>
        public void Detach(TmqClient client)
        {
            client.MessageReceived -= ClientOnMessageReceived;

            lock (_attachedClients)
                _attachedClients.Remove(client);
        }

        /// <summary>
        /// Detach all clients
        /// </summary>
        public void DetachAll()
        {
            lock (_attachedClients)
            {
                foreach (TmqClient client in _attachedClients)
                    client.MessageReceived += ClientOnMessageReceived;

                _attachedClients.Clear();
            }
        }

        /// <summary>
        /// Reads the received model, if there is subscription to the model, trigger the actions.
        /// Use this method when you can't attach the client easily or directly. (ex: use for connections)
        /// </summary>
        public void Read(TmqClient sender, TmqMessage message)
        {
            ClientOnMessageReceived(sender, message);
        }

        /// <summary>
        /// When a message received to Tmq Client, this method will be called
        /// </summary>
        private void ClientOnMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            //only channel messages accepted
            if (message.Type != MessageType.Channel || string.IsNullOrEmpty(message.Target))
                return;

            //find all subscriber actions
            List<QueueSubscription> subs;
            lock (_subscriptions)
            {
                subs = _subscriptions.Where(x => x.ContentType == message.ContentType &&
                                                 x.Channel.Equals(message.Target, StringComparison.InvariantCultureIgnoreCase))
                                     .ToList();
            }

            if (subs.Count == 0)
                return;

            //convert model, only one time to first susbcriber's type
            Type type = subs[0].MessageType;
            object model = _func(message, type);

            //call all subscriber methods if they have same type
            foreach (QueueSubscription sub in subs)
            {
                if (sub.MessageType == type)
                {
                    try
                    {
                        sub.Action.DynamicInvoke(model);
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(message, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            QueueSubscription subscription = new QueueSubscription
                                             {
                                                 Channel = channel,
                                                 ContentType = content,
                                                 MessageType = typeof(T),
                                                 Action = action
                                             };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Unsubscribe from messages in a queue in a channel 
        /// </summary>
        public void Off(string channel, ushort content)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.ContentType == content && x.Channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Clear all subscriptions for the channel
        /// </summary>
        public void Clear(string channel)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Clear all subscription for this instance
        /// </summary>
        public void ClearAll()
        {
            lock (_subscriptions)
                _subscriptions.Clear();
        }
    }
}