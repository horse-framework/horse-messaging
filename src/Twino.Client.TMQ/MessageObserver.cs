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
    public delegate void ReaderExceptionThrownHandler(TwinoMessage message, Exception exception);

    /// <summary>
    /// Type based message reader for TMQ client
    /// </summary>
    public class MessageObserver
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
        private Func<TwinoMessage, Type, object> _func;

        /// <summary>
        /// This event is triggered when user defined action throws an exception
        /// </summary>
        public event ReaderExceptionThrownHandler OnException;

        private readonly ITypeDeliveryContainer _deliveryContainer;

        #endregion

        #region Create

        /// <summary>
        /// Creates new message reader with converter action
        /// </summary>
        public MessageObserver(Func<TwinoMessage, Type, object> func)
        {
            _func = func;
            _deliveryContainer = new TypeDeliveryContainer(new TypeDeliveryResolver());
        }

        /// <summary>
        /// Creates new message reader, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        [Obsolete("This method will be removed in future. Use JsonConsumer instead.")]
        public static MessageObserver JsonReader()
        {
            return JsonConsumer();
        }

        /// <summary>
        /// Creates new message consumer, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        public static MessageObserver JsonConsumer()
        {
            return new MessageObserver((msg, type) =>
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
        public void Read(TmqClient sender, TwinoMessage message)
        {
            ClientOnMessageReceived(sender, message);
        }

        /// <summary>
        /// When a message received to Tmq Client, this method will be called
        /// </summary>
        private async void ClientOnMessageReceived(ClientSocketBase<TwinoMessage> client, TwinoMessage message)
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
                    source = message.WaitResponse ? ReadSource.Request : ReadSource.Direct;
                else
                    return;

                //find all subscriber actions
                List<ReadSubscription> subs = null;
                lock (_subscriptions)
                {
                    switch (source)
                    {
                        case ReadSource.Queue:
                            subs = _subscriptions.Where(x => x.Source == ReadSource.Queue &&
                                                             x.Queue.Equals(message.Target, StringComparison.InvariantCultureIgnoreCase)).ToList();
                            break;

                        case ReadSource.Direct:
                            subs = _subscriptions.Where(x => x.Source == ReadSource.Direct && x.ContentType == message.ContentType).ToList();
                            break;

                        case ReadSource.Request:
                            subs = _subscriptions.Where(x => x.Source == ReadSource.Request && x.ContentType == message.ContentType).ToList();
                            break;
                    }
                }

                if (subs == null || subs.Count == 0)
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

        #region Queue Subscriptions

        /// <summary>
        /// Subscribe to messages of a queue
        /// </summary>
        public void On<T>(Action<T> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.QueueName))
                throw new NullReferenceException("Type doesn't have QueueNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            On(descriptor.QueueName, action);
        }

        /// <summary>
        /// Subscribe to messages from a queue
        /// </summary>
        public void On<T>(string queueName, Action<T> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Queue = queueName,
                                                MessageType = typeof(T),
                                                Action = action
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to messages from a queue
        /// </summary>
        public void On(string queueName, Action<TwinoMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Queue = queueName,
                                                MessageType = null,
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to messages of a queue
        /// </summary>
        public void On<T>(Action<T, TwinoMessage> action)
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.QueueName))
                throw new NullReferenceException("Type doesn't have QueueNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            On(descriptor.QueueName, action);
        }

        /// <summary>
        /// Subscribe to messages of a queue
        /// </summary>
        public void On<T>(string queueName, Action<T, TwinoMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Queue = queueName,
                                                MessageType = typeof(T),
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Unsubscribe from messages of a queue
        /// </summary>
        public void Off<T>()
        {
            TypeDeliveryDescriptor descriptor = _deliveryContainer.GetDescriptor<T>();

            if (descriptor == null)
                throw new ArgumentNullException("Can't resolve TypeDeliveryDescriptor. Use overload method On(string,ushort,T)");

            if (string.IsNullOrEmpty(descriptor.QueueName))
                throw new NullReferenceException("Type doesn't have QueueNameAttribute. Add that attribute to type or use overload method On(string,ushort,T)");

            Off(descriptor.QueueName);
        }

        /// <summary>
        /// Unsubscribe from messages of a queue
        /// </summary>
        public void Off(string queueName)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Queue
                                              && x.Queue.Equals(queueName, StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion

        #region Direct Subscriptions

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
        public void OnDirect(ushort contentType, Action<TwinoMessage> action)
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
        public void OnDirect<T>(Action<T, TwinoMessage> action)
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
        public void OnDirect<T>(ushort contentType, Action<T, TwinoMessage> action)
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

        #endregion

        #region Request Subscriptions

        /// <summary>
        /// Subscribes to handle requests
        /// </summary>
        public void OnRequest<TRequest, TResponse>(ushort contentType, ITwinoRequestHandler<TRequest, TResponse> handler)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Request,
                                                Queue = null,
                                                ContentType = contentType,
                                                MessageType = typeof(TRequest),
                                                Action = null,
                                                TmqMessageParameter = true,
                                                ConsumerExecuter = new RequestHandlerExecuter<TRequest, TResponse>(handler.GetType(), handler, null)
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Unsubscribe from handling requests 
        /// </summary>
        public void OffRequest(ushort contentType)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Request &&
                                              x.ContentType == contentType);
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Clear all subscription for this instance
        /// </summary>
        public void ClearAll()
        {
            lock (_subscriptions)
                _subscriptions.Clear();
        }

        /// <summary>
        /// Returns all subscribed queues
        /// </summary>
        public string[] GetSubscribedQueues()
        {
            List<string> queues = new List<string>();

            lock (_subscriptions)
            {
                foreach (ReadSubscription subscription in _subscriptions)
                {
                    if (subscription.Source != ReadSource.Queue)
                        continue;

                    queues.Add(subscription.Queue);
                }
            }

            return queues.Distinct().ToArray();
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
            List<Type> list = new List<Type>();
            foreach (Type assemblyType in assemblyTypes)
            {
                foreach (Type type in assemblyType.Assembly.GetTypes())
                {
                    List<ModelTypeInfo> types = FindModelTypes(type);
                    foreach (ModelTypeInfo typeInfo in types)
                    {
                        ReadSubscription subscription = CreateConsumerSubscription(typeInfo, consumerFactoryBuilder);
                        if (subscription == null)
                            continue;

                        lock (_subscriptions)
                            _subscriptions.Add(subscription);
                    }

                    if (types.Count > 0)
                        list.Add(type);
                }
            }

            return list;
        }

        /// <summary>
        /// Registers a single consumer
        /// </summary>
        public void RegisterConsumer<TConsumer>(Func<IConsumerFactory> consumerFactoryBuilder = null)
        {
            RegisterConsumer(typeof(TConsumer), consumerFactoryBuilder);
        }

        /// <summary>
        /// Registers a single consumer
        /// </summary>
        public void RegisterConsumer(Type consumerType, Func<IConsumerFactory> consumerFactoryBuilder = null)
        {
            List<ModelTypeInfo> types = FindModelTypes(consumerType);

            foreach (ModelTypeInfo typeInfo in types)
            {
                ReadSubscription subscription = CreateConsumerSubscription(typeInfo, consumerFactoryBuilder);
                if (subscription == null)
                    throw new TypeLoadException("Cant resolve consumer type");

                lock (_subscriptions)
                    _subscriptions.Add(subscription);
            }
        }

        private List<ModelTypeInfo> FindModelTypes(Type consumerType)
        {
            Type openQueueGeneric = typeof(IQueueConsumer<>);
            Type openDirectGeneric = typeof(IDirectConsumer<>);
            Type openRequestGeneric = typeof(ITwinoRequestHandler<,>);

            List<ModelTypeInfo> result = new List<ModelTypeInfo>();

            Type[] interfaceTypes = consumerType.GetInterfaces();
            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsGenericType)
                    continue;

                Type generic = interfaceType.GetGenericTypeDefinition();
                if (openQueueGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ReadSource.Queue, interfaceType.GetGenericArguments().FirstOrDefault()));

                else if (openDirectGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ReadSource.Direct, interfaceType.GetGenericArguments().FirstOrDefault()));

                else if (openRequestGeneric.IsAssignableFrom(generic))
                {
                    Type[] genericArgs = interfaceType.GetGenericArguments();
                    result.Add(new ModelTypeInfo(consumerType, ReadSource.Request, genericArgs[0], genericArgs[1]));
                }
            }

            return result;
        }

        private ReadSubscription CreateConsumerSubscription(ModelTypeInfo typeInfo, Func<IConsumerFactory> consumerFactoryBuilder)
        {
            bool useConsumerFactory = consumerFactoryBuilder != null;

            TypeDeliveryResolver resolver = new TypeDeliveryResolver();
            TypeDeliveryDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType);
            TypeDeliveryDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType);
            var target = GetTarget(typeInfo.Source, consumerDescriptor, modelDescriptor);

            object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

            ConsumerExecuter executer = null;
            switch (typeInfo.Source)
            {
                case ReadSource.Queue:
                {
                    Type executerType = typeof(QueueConsumerExecuter<>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
                    executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }
                case ReadSource.Direct:
                {
                    Type executerType = typeof(DirectConsumerExecuter<>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
                    executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }

                case ReadSource.Request:
                {
                    Type executerType = typeof(RequestHandlerExecuter<,>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType, typeInfo.ResponseType);
                    executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }
            }

            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = typeInfo.Source,
                                                Queue = target.Item1,
                                                ContentType = target.Item2,
                                                MessageType = typeInfo.ModelType,
                                                ResponseType = typeInfo.ResponseType,
                                                Action = null,
                                                ConsumerExecuter = executer
                                            };

            return subscription;
        }

        /// <summary>
        /// Finds target for consumer and model
        /// </summary>
        private Tuple<string, ushort> GetTarget(ReadSource source, TypeDeliveryDescriptor consumerDescriptor, TypeDeliveryDescriptor modelDescriptor)
        {
            ushort contentType = 0;
            string target = null;
            if (source == ReadSource.Queue)
            {
                if (consumerDescriptor.HasQueueName)
                    target = consumerDescriptor.QueueName;
                else if (modelDescriptor.HasQueueName)
                    target = modelDescriptor.QueueName;
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

            if (source == ReadSource.Queue && string.IsNullOrEmpty(target))
                target = modelDescriptor.QueueName;

            return new Tuple<string, ushort>(target, contentType);
        }

        #endregion
    }
}