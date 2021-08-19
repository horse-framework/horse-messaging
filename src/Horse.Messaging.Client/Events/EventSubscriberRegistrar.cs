using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horse.Messaging.Client.Events.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Registrar class for event subscriptions
    /// </summary>
    public class EventSubscriberRegistrar
    {
        private readonly EventOperator _operator;

        /// <summary>
        /// Creates new channem consumer registrar
        /// </summary>
        /// <param name="directOperator"></param>
        public EventSubscriberRegistrar(EventOperator directOperator)
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
                    if (type.IsInterface || type.IsAbstract)
                        continue;

                    List<ModelTypeInfo> types = FindModelTypes(type);
                    foreach (ModelTypeInfo typeInfo in types)
                    {
                        EventSubscriberRegistration registration = CreateHandlerRegistration(typeInfo, consumerFactoryBuilder);
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
        public void RegisterHandler<THandler>(Func<IHandlerFactory> handlerFactoryBuilder = null)
        {
            RegisterHandler(typeof(THandler), handlerFactoryBuilder);
        }

        /// <summary>
        /// Registers a single IDirectReceiver or IRequestHandler
        /// </summary>
        public void RegisterHandler(Type handlerType, Func<IHandlerFactory> handlerFactoryBuilder = null)
        {
            List<ModelTypeInfo> types = FindModelTypes(handlerType);

            foreach (ModelTypeInfo typeInfo in types)
            {
                EventSubscriberRegistration registration = CreateHandlerRegistration(typeInfo, handlerFactoryBuilder);
                if (registration == null)
                    throw new TypeLoadException("Cant resolve handler type");

                lock (_operator.Registrations)
                    _operator.Registrations.Add(registration);
            }
        }

        private List<ModelTypeInfo> FindModelTypes(Type eventType)
        {
            Type implementationType = typeof(IHorseEventHandler);
            List<ModelTypeInfo> result = new List<ModelTypeInfo>();

            Type[] interfaceTypes = eventType.GetInterfaces();
            foreach (Type interfaceType in interfaceTypes)
            {
                if (implementationType.IsAssignableFrom(interfaceType))
                    result.Add(new ModelTypeInfo(eventType, ConsumeSource.Event, interfaceType.GetGenericArguments().FirstOrDefault()));
            }

            return result;
        }

        private EventSubscriberRegistration CreateHandlerRegistration(ModelTypeInfo typeInfo, Func<IHandlerFactory> handlerFactoryBuilder)
        {
            HorseEventAttribute attribute = typeInfo.ConsumerType.GetCustomAttribute<HorseEventAttribute>();
            if (attribute == null)
                throw new ArgumentNullException($"Event handler type does not have HorseEventAttribute: {typeInfo.ConsumerType.FullName}");
            
            bool useConsumerFactory = handlerFactoryBuilder != null;
            object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

            Type executerType = typeof(EventSubscriberExecuter);
            ExecuterBase executer = (ExecuterBase) Activator.CreateInstance(executerType,
                                                                            typeInfo.ConsumerType,
                                                                            consumerInstance,
                                                                            handlerFactoryBuilder);

            EventSubscriberRegistration registration = new EventSubscriberRegistration
            {
                Type = attribute.EventType,
                HandlerType = typeInfo.ConsumerType,
                Executer = executer,
                Target = attribute.Target
            };

            if (executer != null)
                executer.Resolve(registration);

            return registration;
        }
    }
}