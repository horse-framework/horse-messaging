using System;
using System.Collections.Generic;
using System.Linq;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations.Resolvers;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations.Resolvers;
using Horse.Messaging.Client.Queues.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Mq.Client
{
    /// <summary>
    /// Exception thrown handler for message reader
    /// </summary>
    public delegate void ReaderExceptionThrownHandler(HorseMessage message, Exception exception);

    /// <summary>
    /// Type based message reader for HMQ client
    /// </summary>
    public class MessageObserver
    {
        #region Fields

        /// <summary>
        /// All message subscriptions
        /// </summary>
        private readonly List<ConsumeRegistration> _subscriptions = new List<ConsumeRegistration>(16);

        /// <summary>
        /// Attached Horse Clients
        /// </summary>
        private readonly List<HorseClient> _attachedClients = new List<HorseClient>(8);

        /// <summary>
        /// HorseMessage to model type converter function
        /// </summary>
        private Func<HorseMessage, Type, object> _func;

        /// <summary>
        /// This event is triggered when user defined action throws an exception
        /// </summary>
        public event ReaderExceptionThrownHandler OnException;

        private readonly ITypeDeliveryContainer _deliveryContainer;

        /// <summary>
        /// Default configurator for model and consumer types
        /// </summary>
        public ModelTypeConfigurator Configurator { get; set; }

        #endregion

        #region Create

        /// <summary>
        /// Creates new message reader with converter action
        /// </summary>
        public MessageObserver(Func<HorseMessage, Type, object> func)
        {
            _func = func;
            _deliveryContainer = new TypeDeliveryContainer(new TypeDeliveryResolver());
        }

        #endregion

        #region Read

        /// <summary>
        /// Reads the received model, if there is subscription to the model, trigger the actions.
        /// Use this method when you can't attach the client easily or directly. (ex: use for connections)
        /// </summary>
        public void Read(HorseClient sender, HorseMessage message)
        {
            ClientOnMessageReceived(sender, message);
        }

        /// <summary>
        /// When a message received to Horse Client, this method will be called
        /// </summary>
        private async void ClientOnMessageReceived(ClientSocketBase<HorseMessage> client, HorseMessage message)
        {
            try
            {
                ConsumeSource source;

                if (message.Type == MessageType.QueueMessage)
                {
                    if (string.IsNullOrEmpty(message.Target))
                        return;

                    source = ConsumeSource.Queue;
                }
                else if (message.Type == MessageType.DirectMessage)
                    source = message.WaitResponse ? ConsumeSource.Request : ConsumeSource.Direct;
                else
                    return;

                //find all subscriber actions
                List<ConsumeRegistration> subs = null;
                lock (_subscriptions)
                {
                    switch (source)
                    {
                        case ConsumeSource.Queue:
                            subs = _subscriptions.Where(x => x.Source == ConsumeSource.Queue &&
                                                             x.Queue.Equals(message.Target, StringComparison.InvariantCultureIgnoreCase)).ToList();
                            break;

                        case ConsumeSource.Direct:
                            subs = _subscriptions.Where(x => x.Source == ConsumeSource.Direct && x.ContentType == message.ContentType).ToList();
                            break;

                        case ConsumeSource.Request:
                            subs = _subscriptions.Where(x => x.Source == ConsumeSource.Request && x.ContentType == message.ContentType).ToList();

                            //direct consumer waits for ack
                            if (subs.Count == 0)
                                subs = _subscriptions.Where(x => x.Source == ConsumeSource.Direct && x.ContentType == message.ContentType).ToList();
                            break;
                    }
                }

                if (subs == null || subs.Count == 0)
                    return;

                //convert model, only one time to first susbcriber's type
                Type type = subs[0].MessageType;
                object model = type == typeof(string) ? message.GetStringContent() : _func(message, type);

                //call all subscriber methods if they have same type
                foreach (ConsumeRegistration sub in subs)
                {
                    if (sub.MessageType == null || sub.MessageType == type)
                    {
                        try
                        {
                            if (sub.ConsumerExecuter != null)
                                await sub.ConsumerExecuter.Execute((HorseClient) client, message, model);

                            if (sub.Action != null)
                            {
                                if (sub.HmqMessageParameter)
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
                        ConsumeRegistration subscription = CreateConsumerSubscription(typeInfo, consumerFactoryBuilder);
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
                ConsumeRegistration subscription = CreateConsumerSubscription(typeInfo, consumerFactoryBuilder);
                if (subscription == null)
                    throw new TypeLoadException("Cant resolve consumer type");

                lock (_subscriptions)
                    _subscriptions.Add(subscription);
            }
        }

        private List<ModelTypeInfo> FindModelTypes(Type consumerType)
        {
            Type openQueueGeneric = typeof(IQueueConsumer<>);
            Type openDirectGeneric = typeof(IDirectMessageReceiver<>);
            Type openRequestGeneric = typeof(IHorseRequestHandler<,>);

            List<ModelTypeInfo> result = new List<ModelTypeInfo>();

            Type[] interfaceTypes = consumerType.GetInterfaces();
            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsGenericType)
                    continue;

                Type generic = interfaceType.GetGenericTypeDefinition();
                if (openQueueGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Queue, interfaceType.GetGenericArguments().FirstOrDefault()));

                else if (openDirectGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Direct, interfaceType.GetGenericArguments().FirstOrDefault()));

                else if (openRequestGeneric.IsAssignableFrom(generic))
                {
                    Type[] genericArgs = interfaceType.GetGenericArguments();
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Request, genericArgs[0], genericArgs[1]));
                }
            }

            return result;
        }

        private ConsumeRegistration CreateConsumerSubscription(ModelTypeInfo typeInfo, Func<IConsumerFactory> consumerFactoryBuilder)
        {
            bool useConsumerFactory = consumerFactoryBuilder != null;

            TypeDeliveryResolver resolver = new TypeDeliveryResolver();
            TypeDeliveryDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType, null);
            TypeDeliveryDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType, Configurator);
            var target = GetTarget(typeInfo.Source, consumerDescriptor, modelDescriptor);

            object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

            ConsumerExecuter executer = null;
            switch (typeInfo.Source)
            {
                case ConsumeSource.Queue:
                {
                    Type executerType = typeof(QueueConsumerExecuter<>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
                    executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }
                case ConsumeSource.Direct:
                {
                    Type executerType = typeof(DirectConsumerExecuter<>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
                    executer = (ConsumerExecuter) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }

                case ConsumeSource.Request:
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

            if (executer != null)
                executer.Resolve(Configurator);

            ConsumeRegistration subscription = new ConsumeRegistration
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
        private Tuple<string, ushort> GetTarget(ConsumeSource source, TypeDeliveryDescriptor consumerDescriptor, TypeDeliveryDescriptor modelDescriptor)
        {
            ushort contentType = 0;
            string target = null;
            if (source == ConsumeSource.Queue)
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

            if (source == ConsumeSource.Queue && string.IsNullOrEmpty(target))
                target = modelDescriptor.QueueName;

            return new Tuple<string, ushort>(target, contentType);
        }

        #endregion
    }
}