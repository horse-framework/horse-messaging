using System;
using System.Collections.Generic;
using System.Linq;
using Horse.Messaging.Client.Annotations.Resolvers;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations.Resolvers;
using Horse.Messaging.Client.Queues.Internal;

namespace Horse.Messaging.Client.Internal
{
    internal class ConsumeRegistrar
    {


        public void Register(HorseClient client)
        {
        }
        
        
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

    }
}