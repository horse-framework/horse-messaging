using System;
using System.Collections.Generic;
using System.Linq;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Direct
{
    internal class DirectConsumerRegistrar
    {
        private readonly DirectOperator _operator;

        public DirectTypeDescriptor DefaultDescriptor { get; set; }

        
        internal DirectConsumerRegistrar(DirectOperator directOperator)
        {
            _operator = directOperator;
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
                        DirectConsumeRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
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
                DirectConsumeRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
                if (registration == null)
                    throw new TypeLoadException("Cant resolve consumer type");

                lock (_operator.Registrations)
                    _operator.Registrations.Add(registration);
            }
        }

        private List<ModelTypeInfo> FindModelTypes(Type consumerType)
        {
            Type openDirectGeneric = typeof(IDirectConsumer<>);
            Type openRequestGeneric = typeof(IHorseRequestHandler<,>);

            List<ModelTypeInfo> result = new List<ModelTypeInfo>();

            Type[] interfaceTypes = consumerType.GetInterfaces();
            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsGenericType)
                    continue;

                Type generic = interfaceType.GetGenericTypeDefinition();

                if (openDirectGeneric.IsAssignableFrom(generic))
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Direct, interfaceType.GetGenericArguments().FirstOrDefault()));

                else if (openRequestGeneric.IsAssignableFrom(generic))
                {
                    Type[] genericArgs = interfaceType.GetGenericArguments();
                    result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Request, genericArgs[0], genericArgs[1]));
                }
            }

            return result;
        }

        private DirectConsumeRegistration CreateConsumerRegistration(ModelTypeInfo typeInfo, Func<IConsumerFactory> consumerFactoryBuilder)
        {
            bool useConsumerFactory = consumerFactoryBuilder != null;

            DirectTypeResolver resolver = new DirectTypeResolver();
            DirectTypeDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType, DefaultDescriptor);
            DirectTypeDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType, DefaultDescriptor);
            
            object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

            ExecuterBase executer = null;
            switch (typeInfo.Source)
            {
                case ConsumeSource.Direct:
                {
                    Type executerType = typeof(DirectConsumerExecuter<>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
                    executer = (ExecuterBase) Activator.CreateInstance(executerGenericType,
                                                                           typeInfo.ConsumerType,
                                                                           consumerInstance,
                                                                           consumerFactoryBuilder);
                    break;
                }

                case ConsumeSource.Request:
                {
                    Type executerType = typeof(RequestHandlerExecuter<,>);
                    Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType, typeInfo.ResponseType);
                    executer = (ExecuterBase) Activator.CreateInstance(executerGenericType,
                                                                       typeInfo.ConsumerType,
                                                                       consumerInstance,
                                                                       consumerFactoryBuilder);
                    break;
                }
            }

            ushort contentType = modelDescriptor.ContentType.HasValue ? modelDescriptor.ContentType.Value : (ushort) 0;
            if (consumerDescriptor.ContentType.HasValue)
                contentType = consumerDescriptor.ContentType.Value;
                
            DirectConsumeRegistration registration = new DirectConsumeRegistration
                                                     {
                                                         ContentType = contentType,
                                                         MessageType = typeInfo.ModelType,
                                                         ResponseType = typeInfo.ResponseType,
                                                         ConsumerType = typeInfo.ConsumerType,
                                                         ConsumerExecuter = executer
                                                     };

            if (executer != null)
                executer.Resolve(registration);
            
            return registration;
        }

    }
}