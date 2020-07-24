using System;
using System.Collections.Generic;
using System.Linq;
using Twino.Client.TMQ.Annotations.Resolvers;
using Twino.Client.TMQ.Internal;
using Twino.Client.TMQ.Models;
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
    public class MessageConsumer
    {
        #region Fields

        /// <summary>
        /// All message subscriptions
        /// </summary>
        private readonly List<ReadSubscription> _subscriptions = new List<ReadSubscription>(16);

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

        private ITypeDeliveryContainer _deliveryContainer;

        #endregion

        #region Create

        /// <summary>
        /// Creates new message reader with converter action
        /// </summary>
        public MessageConsumer(Func<TmqMessage, Type, object> func)
        {
            _func = func;
            _deliveryContainer = new TypeDeliveryContainer(new TypeDeliveryResolver());
        }

        /// <summary>
        /// Creates new message reader, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        [Obsolete("This method will be removed in future. Use JsonConsumer instead.")]
        public static MessageConsumer JsonReader()
        {
            return JsonConsumer();
        }

        /// <summary>
        /// Creates new message consumer, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        public static MessageConsumer JsonConsumer()
        {
            return new MessageConsumer((msg, type) =>
            {
                if (msg.Content == null || msg.Length < 1)
                    return null;

                return System.Text.Json.JsonSerializer.Deserialize(msg.ToString(), type);
            });
        }

        #endregion

        #region Attach - Detach

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

        #endregion

        #region Read

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
        private async void ClientOnMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            try
            {
                ReadSource source;

                if (message.Type == MessageType.QueueMessage)
                {
                    if (string.IsNullOrEmpty(message.Target))
                        return;

                    source = ReadSource.Queue;
                }
                else if (message.Type == MessageType.DirectMessage)
                    source = ReadSource.Direct;
                else
                    return;

                //find all subscriber actions
                List<ReadSubscription> subs;
                lock (_subscriptions)
                {
                    if (source == ReadSource.Direct)
                        subs = _subscriptions.Where(x => x.Source == ReadSource.Direct && x.ContentType == message.ContentType)
                                             .ToList();
                    else
                        subs = _subscriptions.Where(x => x.Source == ReadSource.Queue &&
                                                         x.ContentType == message.ContentType &&
                                                         x.Channel.Equals(message.Target, StringComparison.InvariantCultureIgnoreCase))
                                             .ToList();
                }

                if (subs.Count == 0)
                    return;

                //convert model, only one time to first susbcriber's type
                Type type = subs[0].MessageType;
                object model = type == typeof(string) ? message.GetStringContent() : _func(message, type);

                //call all subscriber methods if they have same type
                foreach (ReadSubscription sub in subs)
                {
                    if (sub.MessageType == null || sub.MessageType == type)
                    {
                        try
                        {
                            if (sub.ConsumerExecuter != null)
                                await sub.ConsumerExecuter.Execute((TmqClient) client, message, model);

                            if (sub.Action != null)
                            {
                                if (sub.TmqMessageParameter)
                                {
                                    if (sub.MessageType == null)
                                        sub.Action.DynamicInvoke(message);
                                    else
                                        sub.Action.DynamicInvoke(model, message);
                                }
                                else
                                    sub.Action.DynamicInvoke(model);
                            }
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(message, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(message, ex);
            }
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(Action<T> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.ChannelName))
                throw new NullReferenceException("Type doesn't have ChannelNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            if (!descriptor.QueueId.HasValue)
                throw new NullReferenceException("Type doesn't have QueueIdAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            On(descriptor.ChannelName, descriptor.QueueId.Value, action);
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(string channel, ushort queueId, Action<T> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Channel = channel,
                                                ContentType = queueId,
                                                MessageType = typeof(T),
                                                Action = action
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On(string channel, ushort queueId, Action<TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Channel = channel,
                                                ContentType = queueId,
                                                MessageType = null,
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(Action<T, TmqMessage> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.ChannelName))
                throw new NullReferenceException("Type doesn't have ChannelNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            if (!descriptor.QueueId.HasValue)
                throw new NullReferenceException("Type doesn't have QueueIdAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            On(descriptor.ChannelName, descriptor.QueueId.Value, action);
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(string channel, ushort queueId, Action<T, TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Channel = channel,
                                                ContentType = queueId,
                                                MessageType = typeof(T),
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(Action<T> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(ushort,T)");

            if (!descriptor.ContentType.HasValue)
                throw new NullReferenceException("Type doesn't have ContentTypeAttribute. Add that attribute to type or use overload method On(ushort,T)");

            OnDirect(descriptor.ContentType.Value, action);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(ushort contentType, Action<T> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Direct,
                                                ContentType = contentType,
                                                MessageType = typeof(T),
                                                Action = action
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect(ushort contentType, Action<TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Direct,
                                                ContentType = contentType,
                                                MessageType = null,
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(Action<T, TmqMessage> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(ushort,T)");

            if (!descriptor.ContentType.HasValue)
                throw new NullReferenceException("Type doesn't have ContentTypeAttribute. Add that attribute to type or use overload method On(ushort,T)");

            OnDirect(descriptor.ContentType.Value, action);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(ushort contentType, Action<T, TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Direct,
                                                ContentType = contentType,
                                                MessageType = typeof(T),
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Unsubscribe from messages in a queue in a channel 
        /// </summary>
        public void Off<T>()
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.ChannelName))
                throw new NullReferenceException("Type doesn't have ChannelNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            if (!descriptor.QueueId.HasValue)
                throw new NullReferenceException("Type doesn't have QueueIdAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            Off(descriptor.ChannelName, descriptor.QueueId.Value);
        }

        /// <summary>
        /// Unsubscribe from messages in a queue in a channel 
        /// </summary>
        public void Off(string channel, ushort queueId)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Queue &&
                                              x.ContentType == queueId
                                              && x.Channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Unsubscribe from direct messages 
        /// </summary>
        public void OffDirect<T>()
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(ushort,T)");

            if (!descriptor.ContentType.HasValue)
                throw new NullReferenceException("Type doesn't have ContentTypeAttribute. Add that attribute to type or use overload method On(ushort,T)");

            OffDirect(descriptor.ContentType.Value);
        }

        /// <summary>
        /// Unsubscribe from direct messages with a content type 
        /// </summary>
        public void OffDirect(ushort contentType)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Direct && x.ContentType == contentType);
        }

        /// <summary>
        /// Clear all subscriptions for the channels and direct messages
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

        /// <summary>
        /// Returns all subscribed channels
        /// </summary>
        public string[] GetSubscribedChannels()
        {
            List<string> channels = new List<string>();

            lock (_subscriptions)
            {
                foreach (ReadSubscription subscription in _subscriptions)
                {
                    if (subscription.Source != ReadSource.Queue)
                        continue;

                    channels.Add(subscription.Channel);
                }
            }

            return channels.Distinct().ToArray();
        }

        #endregion

        #region Consumer Registration

        /// <summary>
        /// Registers all IQueueConsumers in assemblies
        /// </summary>
        public IEnumerable<Type> RegisterAssemblyConsumers(params Type[] assemblyTypes)
        {
            return RegisterAssemblyConsumers(null, assemblyTypes);
        }

        /// <summary>
        /// Registers all IQueueConsumers in assemblies
        /// </summary>
        public IEnumerable<Type> RegisterAssemblyConsumers(Func<IConsumerFactory> consumerFactoryBuilder, params Type[] assemblyTypes)
        {
            Type openQueueGeneric = typeof(IQueueConsumer<>);
            Type openDirectGeneric = typeof(IDirectConsumer<>);
            Type executerType = typeof(ConsumerExecuter<>);
            bool useConsumerFactory = consumerFactoryBuilder != null;

            foreach (Type assemblyType in assemblyTypes)
            {
                foreach (Type type in assemblyType.Assembly.GetTypes())
                {
                    bool queue = false;
                    Type modelType = null;

                    Type[] interfaceTypes = type.GetInterfaces();
                    foreach (Type interfaceType in interfaceTypes)
                    {
                        if (!interfaceType.IsGenericType)
                            continue;

                        Type generic = interfaceType.GetGenericTypeDefinition();
                        if (openQueueGeneric.IsAssignableFrom(generic))
                        {
                            modelType = interfaceType.GetGenericArguments().FirstOrDefault();
                            queue = true;
                            break;
                        }

                        if (openDirectGeneric.IsAssignableFrom(generic))
                        {
                            modelType = interfaceType.GetGenericArguments().FirstOrDefault();
                            break;
                        }
                    }

                    if (modelType == null)
                        continue;

                    TypeDeliveryResolver resolver = new TypeDeliveryResolver();
                    TypeDeliveryDescriptor consumerDescriptor = resolver.Resolve(type);
                    TypeDeliveryDescriptor modelDescriptor = resolver.Resolve(modelType);
                    var target = GetTarget(queue, consumerDescriptor, modelDescriptor);
                    if (string.IsNullOrEmpty(target.Item1))
                        continue;

                    object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(type);
                    Type executerGenericType = executerType.MakeGenericType(modelType);

                    ConsumerExecuter executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType, type, consumerInstance, consumerFactoryBuilder);

                    ReadSubscription subscription = new ReadSubscription
                                                    {
                                                        Source = queue ? ReadSource.Queue : ReadSource.Direct,
                                                        Channel = target.Item1,
                                                        ContentType = target.Item2,
                                                        MessageType = modelType,
                                                        Action = null,
                                                        ConsumerExecuter = executer
                                                    };

                    lock (_subscriptions)
                        _subscriptions.Add(subscription);

                    yield return type;
                }
            }
        }

        /// <summary>
        /// Finds target for consumer and model
        /// </summary>
        private Tuple<string, ushort> GetTarget(bool isQueue, TypeDeliveryDescriptor consumerDescriptor, TypeDeliveryDescriptor modelDescriptor)
        {
            ushort contentType = 0;
            string target = null;
            if (isQueue)
            {
                if (consumerDescriptor.HasQueueId)
                    contentType = consumerDescriptor.QueueId ?? 0;
                else if (modelDescriptor.HasQueueId)
                    contentType = modelDescriptor.QueueId ?? 0;

                if (consumerDescriptor.HasChannelName)
                    target = consumerDescriptor.ChannelName;
                else if (modelDescriptor.HasChannelName)
                    target = modelDescriptor.ChannelName;
            }
            else
            {
                if (consumerDescriptor.HasContentType)
                    contentType = consumerDescriptor.ContentType ?? 0;
                else if (modelDescriptor.HasContentType)
                    contentType = modelDescriptor.ContentType ?? 0;

                if (consumerDescriptor.HasDirectReceiver)
                    target = consumerDescriptor.DirectTarget;
                else if (modelDescriptor.HasDirectReceiver)
                    target = modelDescriptor.DirectTarget;
            }

            return new Tuple<string, ushort>(target, contentType);
        }

        #endregion
    }
}