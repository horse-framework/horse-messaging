using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Queues
{
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
			List<ModelTypeInfo> types = FindModelTypes(consumerType);

			foreach (ModelTypeInfo typeInfo in types)
			{
				QueueConsumerRegistration registration = CreateConsumerRegistration(typeInfo, consumerFactoryBuilder);
				if (registration == null)
					throw new TypeLoadException("Cant resolve consumer type");

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


			Type executerType = typeof(QueueConsumerExecuter<>);
			Type executerGenericType = executerType.MakeGenericType(typeInfo.ModelType);
			ExecuterBase executer = (ExecuterBase) Activator.CreateInstance(executerGenericType,
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
				ConsumerExecuter = executer
			};

			registration.IntercetorDescriptors.AddRange(ResolveInterceptorAttributes(typeInfo, !useConsumerFactory));

			executer?.Resolve(registration);

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
}