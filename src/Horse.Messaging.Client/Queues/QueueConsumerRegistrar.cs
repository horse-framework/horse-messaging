using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Registrar for IQueueConsumer implementations
/// </summary>
public class QueueConsumerRegistrar
{
    private readonly QueueOperator _operator;

    /// <summary>
    /// Default options for queue consumer registration
    /// </summary>
    public QueueTypeDescriptor DefaultDescriptor { get; set; }

    /// <summary>
    /// Creates new queue consumer registrar
    /// </summary>
    public QueueConsumerRegistrar(QueueOperator directOperator)
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
    /// Registers a single consumer
    /// </summary>
    public void RegisterConsumer<TConsumer>(Func<IHandlerFactory> consumerFactoryBuilder = null)
    {
        RegisterConsumer(typeof(TConsumer), consumerFactoryBuilder);
    }

    /// <summary>
    /// Registers a single consumer with explicit partition options.
    /// The <paramref name="partitionLabel"/> overrides any
    /// <see cref="Annotations.PartitionedQueueAttribute"/> present on the consumer or model type.
    /// </summary>
    /// <param name="consumerFactoryBuilder">Optional factory for DI-based consumer creation.</param>
    /// <param name="partitionLabel">
    /// Routing label for the partition. Use <c>null</c> to keep the value resolved from
    /// attributes (or no partition). Use <see cref="string.Empty"/> for label-less
    /// partitioned subscribe (round-robin path).
    /// </param>
    /// <param name="maxPartitions">Maximum partitions for auto-create. <c>null</c> = not set (server default), <c>0</c> = unlimited.</param>
    /// <param name="subscribersPerPartition">Max subscribers per partition. <c>null</c> = not set (server default).</param>
    public void RegisterConsumer<TConsumer>(Func<IHandlerFactory> consumerFactoryBuilder, string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null)
    {
        RegisterConsumer(typeof(TConsumer), consumerFactoryBuilder, partitionLabel, maxPartitions, subscribersPerPartition);
    }

    /// <summary>
    /// Registers all IQueueConsumers in assemblies
    /// </summary>
    public IEnumerable<Type> RegisterAssemblyConsumers(Func<IHandlerFactory> consumerFactoryBuilder, params Type[] assemblyTypes)
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
                    QueueConsumerRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
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
    public void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder = null)
    {
        RegisterConsumer(consumerType, consumerFactoryBuilder, queueName: null, partitionLabel: null, null, null, overridePartition: false);
    }

    /// <summary>
    /// Registers a single consumer with explicit partition options that override any attributes on the type.
    /// </summary>
    public void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder,
        string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null)
    {
        RegisterConsumer(consumerType, consumerFactoryBuilder, queueName: null, partitionLabel, maxPartitions, subscribersPerPartition, overridePartition: true);
    }

    /// <summary>
    /// Registers a single consumer with an explicit queue name override and optional partition options.
    /// </summary>
    public void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder,
        string queueName, string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null)
    {
        RegisterConsumer(consumerType, consumerFactoryBuilder, queueName, partitionLabel, maxPartitions, subscribersPerPartition, overridePartition: partitionLabel != null);
    }

    /// <summary>
    /// Registers a single consumer with a queue name transform and an explicit partition label.
    /// The transform receives the original queue name (resolved from attributes) and returns the final name.
    /// The consumer subscribes as a partitioned consumer with the given label.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="partitionLabel"/> is null or empty.
    /// Use the <c>enterWorkerPool</c> overload to enter the worker pool without a label.</exception>
    public void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder,
        Func<string, string> queueNameTransform, string partitionLabel)
    {
        if (string.IsNullOrEmpty(partitionLabel))
            throw new ArgumentException(
                "Partition label cannot be null or empty. " +
                "To enter the auto-assign worker pool without a label, use the enterWorkerPool overload instead.",
                nameof(partitionLabel));

        List<ModelTypeInfo> types = FindModelTypes(consumerType);

        foreach (ModelTypeInfo typeInfo in types)
        {
            QueueConsumerRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
            if (registration == null)
                throw new TypeLoadException("Cant resolve consumer type");

            if (queueNameTransform != null)
                registration.QueueName = queueNameTransform(registration.QueueName);

            registration.PartitionLabel = partitionLabel;
            registration.MaxPartitions = null;
            registration.SubscribersPerPartition = null;

            lock (_operator.Registrations)
                _operator.Registrations.Add(registration);
        }
    }

    /// <summary>
    /// Registers a single consumer with a queue name transform function.
    /// The transform receives the original queue name (resolved from attributes) and returns the final name.
    /// When <paramref name="enterWorkerPool"/> is true, the consumer subscribes as a partitioned worker
    /// without a label — it enters the auto-assign worker pool so the server can dynamically assign
    /// it to newly created partitions.
    /// </summary>
    public void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder,
        Func<string, string> queueNameTransform, bool enterWorkerPool)
    {
        List<ModelTypeInfo> types = FindModelTypes(consumerType);

        foreach (ModelTypeInfo typeInfo in types)
        {
            QueueConsumerRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
            if (registration == null)
                throw new TypeLoadException("Cant resolve consumer type");

            if (queueNameTransform != null)
                registration.QueueName = queueNameTransform(registration.QueueName);

            if (enterWorkerPool)
            {
                // Empty string = label-less partitioned subscribe (worker pool entry)
                registration.PartitionLabel = string.Empty;
                registration.MaxPartitions = null;
                registration.SubscribersPerPartition = null;
            }

            lock (_operator.Registrations)
                _operator.Registrations.Add(registration);
        }
    }

    private void RegisterConsumer(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder,
        string queueName, string partitionLabel, int? maxPartitions, int? subscribersPerPartition, bool overridePartition)
    {
        List<ModelTypeInfo> types = FindModelTypes(consumerType);

        foreach (ModelTypeInfo typeInfo in types)
        {
            QueueConsumerRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
            if (registration == null)
                throw new TypeLoadException("Cant resolve consumer type");

            // Override queue name if explicitly provided
            if (!string.IsNullOrEmpty(queueName))
                registration.QueueName = queueName;

            // Builder-level partition settings override attribute-level ones
            if (overridePartition)
            {
                registration.PartitionLabel = partitionLabel ?? string.Empty;
                registration.MaxPartitions = maxPartitions;
                registration.SubscribersPerPartition = subscribersPerPartition;
            }

            lock (_operator.Registrations)
                _operator.Registrations.Add(registration);
        }
    }

    private List<ModelTypeInfo> FindModelTypes(Type consumerType)
    {
        Type openQueueGeneric = typeof(IQueueConsumer<>);
        List<ModelTypeInfo> result = new List<ModelTypeInfo>();

        Type[] interfaceTypes = consumerType.GetInterfaces();
        foreach (Type interfaceType in interfaceTypes)
        {
            if (!interfaceType.IsGenericType)
                continue;

            Type generic = interfaceType.GetGenericTypeDefinition();
            if (openQueueGeneric.IsAssignableFrom(generic))
                result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Queue, interfaceType.GetGenericArguments().FirstOrDefault()));
        }

        return result;
    }

    private QueueConsumerRegistration CreateConsumerRegistration(ModelTypeInfo typeInfo, Func<IHandlerFactory> consumerFactoryBuilder)
    {
        bool useConsumerFactory = consumerFactoryBuilder != null;

        QueueTypeResolver resolver = new QueueTypeResolver(_operator.Client);
        QueueTypeDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType, null);
        QueueTypeDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType, DefaultDescriptor);

        object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);


        Type executorType = typeof(QueueConsumerExecutor<>);
        Type executorGenericType = executorType.MakeGenericType(typeInfo.ModelType);
        ExecutorBase executor = (ExecutorBase) Activator.CreateInstance(executorGenericType,
            typeInfo.ConsumerType,
            consumerInstance,
            consumerFactoryBuilder);

        string queueName = modelDescriptor.QueueName;

        if (consumerDescriptor.HasQueueName && !string.IsNullOrEmpty(consumerDescriptor.QueueName))
            queueName = consumerDescriptor.QueueName;

        QueueConsumerRegistration registration = new QueueConsumerRegistration
        {
            QueueName = queueName,
            MessageType = typeInfo.ModelType,
            ConsumerType = typeInfo.ConsumerType,
            ConsumerExecuter = executor
        };

        // ── Partition metadata from consumer-level attributes ─────────────────
        // PartitionLabel: consumer descriptor wins, fall back to model descriptor
        if (consumerDescriptor.PartitionLabel != null)
        {
            registration.PartitionLabel = consumerDescriptor.PartitionLabel;
            registration.MaxPartitions = consumerDescriptor.MaxPartitions;
            registration.SubscribersPerPartition = consumerDescriptor.SubscribersPerPartition;
        }
        else if (modelDescriptor.PartitionLabel != null)
        {
            registration.PartitionLabel = modelDescriptor.PartitionLabel;
            registration.MaxPartitions = modelDescriptor.MaxPartitions;
            registration.SubscribersPerPartition = modelDescriptor.SubscribersPerPartition;
        }

        registration.InterceptorDescriptors.AddRange(ResolveInterceptorAttributes(typeInfo, !useConsumerFactory));

        executor?.Resolve(registration);

        return registration;
    }

    private static IEnumerable<InterceptorTypeDescriptor> ResolveInterceptorAttributes(ModelTypeInfo typeInfo, bool createInstance)
    {
        List<InterceptorAttribute> consumerInterceptors = typeInfo.ConsumerType.GetCustomAttributes<InterceptorAttribute>(true).ToList();
        List<InterceptorAttribute> modelInterceptors = typeInfo.ModelType.GetCustomAttributes<InterceptorAttribute>(true).ToList();

        foreach (InterceptorAttribute modelInterceptor in modelInterceptors)
        {
            if (consumerInterceptors.Any(m => m.InterceptorType.IsAssignableTo(modelInterceptor.InterceptorType))) continue;
            if (consumerInterceptors.Any(m => m.InterceptorType.IsAssignableFrom(modelInterceptor.InterceptorType))) continue;
            consumerInterceptors.Add(modelInterceptor);
        }

        return consumerInterceptors.OrderBy(m => m.Order).Select(m => InterceptorTypeDescriptor.Create(m, createInstance));
    }
}