using System;
using System.Collections.Generic;
using System.Linq;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Channels
{
    internal class ChannelConsumerRegistrar
    {
        private readonly ChannelOperator _operator;

        public ChannelTypeDescriptor DefaultDescriptor { get; set; }

        internal ChannelConsumerRegistrar(ChannelOperator directOperator)
        {
            _operator = directOperator;
        }

        /// <summary>
        /// Registers all IDirectReceiver and IRequestHandler types in assemblies
        /// </summary>
        public IEnumerable<Type> RegisterAssemblyHandlers(params Type[] assemblyTypes)
        {
            return RegisterAssemblyHandlers(null, assemblyTypes);
        }

        /// <summary>
        /// Registers all IDirectReceiver and IRequestHandler types in assemblies
        /// </summary>
        public IEnumerable<Type> RegisterAssemblyHandlers(Func<IHandlerFactory> consumerFactoryBuilder, params Type[] assemblyTypes)
        {
            List<Type> list = new List<Type>();
            foreach (Type assemblyType in assemblyTypes)
            {
                foreach (Type type in assemblyType.Assembly.GetTypes())
                {
                    List<ModelTypeInfo> types = FindModelTypes(type);
                    foreach (ModelTypeInfo typeInfo in types)
                    {
                        ChannelSubscriberRegistration registration = CreateHandlerRegistration(typeInfo, consumerFactoryBuilder);
                        if (registration == null)
                            continue;

                        lock (_operator.Registrations)
                            _operator.Registrations.Add(registration);
                    }

                    if (types.Count > 0)
                        list.Add(type);
                }
            }

            return list;
        }

        /// <summary>
        /// Registers a single IDirectReceiver or IRequestHandler
        /// </summary>
        public void RegisterHandler<THandler>(Func<IHandlerFactory> consumerFactoryBuilder = null)
        {
            RegisterHandler(typeof(THandler), consumerFactoryBuilder);
        }

        /// <summary>
        /// Registers a single IDirectReceiver or IRequestHandler
        /// </summary>
        public void RegisterHandler(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder = null)
        {
            List<ModelTypeInfo> types = FindModelTypes(consumerType);

            foreach (ModelTypeInfo typeInfo in types)
            {
                ChannelSubscriberRegistration registration = CreateHandlerRegistration(typeInfo, consumerFactoryBuilder);
                if (registration == null)
                    throw new TypeLoadException("Cant resolve consumer type");

                lock (_operator.Registrations)
                    _operator.Registrations.Add(registration);
            }
        }

        private List<ModelTypeInfo> FindModelTypes(Type consumerType)
        {
            Type openDirectGeneric = typeof(IChannelSubscriber<>);
            List<ModelTypeInfo> result = new List<ModelTypeInfo>();

            Type[] interfaceTypes = consumerType.GetInterfaces();
            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsGenericType)
                    continue;

                Type generic = interfaceType.GetGenericTypeDefinition();

                if (openDirectGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Channel, interfaceType.GetGenericArguments().FirstOrDefault()));
            }

            return result;
        }

        private ChannelSubscriberRegistration CreateHandlerRegistration(ModelTypeInfo typeInfo, Func<IHandlerFactory> consumerFactoryBuilder)
        {
            bool useConsumerFactory = consumerFactoryBuilder != null;

            ChannelTypeResolver resolver = new ChannelTypeResolver();
            ChannelTypeDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType, DefaultDescriptor);
            ChannelTypeDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType, DefaultDescriptor);

            object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

            Type executerType = typeof(ChannelConsumerExecuter);
            Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType, typeInfo.ResponseType);
            ExecuterBase executer = (ExecuterBase) Activator.CreateInstance(executerGenericType,
                                                                            typeInfo.ConsumerType,
                                                                            consumerInstance,
                                                                            consumerFactoryBuilder);

            string channelName = modelDescriptor.Name;
            if (!string.IsNullOrEmpty(consumerDescriptor.Name))
                channelName = consumerDescriptor.Name;

            ChannelSubscriberRegistration registration = new ChannelSubscriberRegistration
            {
                Name = channelName,
                MessageType = typeInfo.ModelType,
                Executer = executer
            };

            if (executer != null)
                executer.Resolve(registration);

            return registration;
        }
    }
}