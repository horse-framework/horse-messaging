using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Direct
{
	/// <summary>
	///     Registrar object for IDirectHandler and IRequestHandler implementations
	/// </summary>
	public class DirectHandlerRegistrar
	{
		private readonly DirectOperator _operator;

		/// <summary>
		///     Default options for direct types
		/// </summary>
		public DirectTypeDescriptor DefaultDescriptor { get; set; }

		/// <summary>
		///     Creates new direct handler registrar
		/// </summary>
		public DirectHandlerRegistrar(DirectOperator directOperator)
		{
			_operator = directOperator;
		}

		/// <summary>
		///     Registers all IDirectReceiver and IRequestHandler types in assemblies
		/// </summary>
		public IEnumerable<Type> RegisterAssemblyHandlers(params Type[] assemblyTypes)
		{
			return RegisterAssemblyHandlers(null, assemblyTypes);
		}

		/// <summary>
		///     Registers all IDirectReceiver and IRequestHandler types in assemblies
		/// </summary>
		public IEnumerable<Type> RegisterAssemblyHandlers(Func<IHandlerFactory> consumerFactoryBuilder, params Type[] assemblyTypes)
		{
			List<Type> list = new();

			foreach (Type assemblyType in assemblyTypes)
			foreach (Type type in assemblyType.Assembly.GetTypes())
			{
				if (type.IsInterface || type.IsAbstract)
					continue;

				List<ModelTypeInfo> types = FindModelTypes(type);
				foreach (ModelTypeInfo typeInfo in types)
				{
					DirectHandlerRegistration registration = CreateHandlerRegistration(typeInfo, consumerFactoryBuilder);
					if (registration == null)
						continue;

					lock (_operator.Registrations)
					{
						_operator.Registrations.Add(registration);
					}
				}

				if (types.Count > 0) list.Add(type);
			}

			return list;
		}

		/// <summary>
		///     Registers a single IDirectReceiver or IRequestHandler
		/// </summary>
		public void RegisterHandler<THandler>(Func<IHandlerFactory> consumerFactoryBuilder = null)
		{
			RegisterHandler(typeof(THandler), consumerFactoryBuilder);
		}

		/// <summary>
		///     Registers a single IDirectReceiver or IRequestHandler
		/// </summary>
		public void RegisterHandler(Type consumerType, Func<IHandlerFactory> consumerFactoryBuilder = null)
		{
			List<ModelTypeInfo> types = FindModelTypes(consumerType);

			foreach (ModelTypeInfo typeInfo in types)
			{
				DirectHandlerRegistration registration = CreateHandlerRegistration(typeInfo, consumerFactoryBuilder);
				if (registration == null)
					throw new TypeLoadException("Cant resolve consumer type");

				lock (_operator.Registrations)
				{
					_operator.Registrations.Add(registration);
				}
			}
		}

		private List<ModelTypeInfo> FindModelTypes(Type consumerType)
		{
			Type openDirectGeneric = typeof(IDirectMessageHandler<>);
			Type openRequestGeneric = typeof(IHorseRequestHandler<,>);

			List<ModelTypeInfo> result = new();

			Type[] interfaceTypes = consumerType.GetInterfaces();
			foreach (Type interfaceType in interfaceTypes)
			{
				if (!interfaceType.IsGenericType)
					continue;

				Type generic = interfaceType.GetGenericTypeDefinition();

				if (openDirectGeneric.IsAssignableFrom(generic))
				{
					result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Direct, interfaceType.GetGenericArguments().FirstOrDefault()));
				}

				else if (openRequestGeneric.IsAssignableFrom(generic))
				{
					Type[] genericArgs = interfaceType.GetGenericArguments();
					result.Add(new ModelTypeInfo(consumerType, ConsumeSource.Request, genericArgs[0], genericArgs[1]));
				}
			}

			return result;
		}

		private DirectHandlerRegistration CreateHandlerRegistration(ModelTypeInfo typeInfo, Func<IHandlerFactory> consumerFactoryBuilder)
		{
			bool useConsumerFactory = consumerFactoryBuilder != null;

			DirectTypeResolver resolver = new();
			DirectTypeDescriptor consumerDescriptor = resolver.Resolve(typeInfo.ConsumerType, DefaultDescriptor);
			DirectTypeDescriptor modelDescriptor = resolver.Resolve(typeInfo.ModelType, DefaultDescriptor);

			object consumerInstance = useConsumerFactory ? null : Activator.CreateInstance(typeInfo.ConsumerType);

			ExecuterBase executer = null;
			switch (typeInfo.Source)
			{
				case ConsumeSource.Direct:
				{
					Type executerType = typeof(DirectHandlerExecuter<>);
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

			DirectHandlerRegistration registration = new()
			{
				ContentType = contentType,
				MessageType = typeInfo.ModelType,
				ResponseType = typeInfo.ResponseType,
				ConsumerType = typeInfo.ConsumerType,
				ConsumerExecuter = executer
			};
			
			registration.IntercetorDescriptors.AddRange(ResolveInterceptorAttributes(typeInfo, !useConsumerFactory));
			executer?.Resolve(registration);

			return registration;
		}

		private static IEnumerable<InterceptorTypeDescriptor> ResolveInterceptorAttributes(ModelTypeInfo typeInfo, bool createInstance)
		{
			List<InterceptorAttribute> consumerInterceptors = typeInfo.ConsumerType.GetCustomAttributes<InterceptorAttribute>().ToList();
			List<InterceptorAttribute> modelInterceptors = typeInfo.ModelType.GetCustomAttributes<InterceptorAttribute>().ToList();

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