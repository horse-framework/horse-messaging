using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Internal
{
	/// <summary>
	/// Base class for executers
	/// </summary>
	public abstract class ExecuterBase
	{
		/// <summary>
		/// If true, sends acknowledge when execute operation is completed successfuly
		/// </summary>
		protected bool SendPositiveResponse { get; set; }

		/// <summary>
		/// If true, sends negative acknowledge when execute operation throws an exception
		/// </summary>
		protected bool SendNegativeResponse { get; set; }

		/// <summary>
		/// If Negative acknowledge is sending, this value is the reason for it.
		/// </summary>
		protected NegativeReason NegativeReason { get; set; }

		/// <summary>
		/// Execution retry attribute
		/// </summary>
		protected RetryAttribute Retry { get; private set; }

		/// <summary>
		/// Default push exception descriptors
		/// </summary>
		protected TransportExceptionDescriptor DefaultPushException { get; private set; }

		/// <summary>
		/// Additional push exception descriptors
		/// </summary>
		protected List<TransportExceptionDescriptor> PushExceptions { get; } = new();

		/// <summary>
		/// Default publish exception descriptors
		/// </summary>
		protected TransportExceptionDescriptor DefaultPublishException { get; private set; }

		/// <summary>
		/// Additional publish exception descriptors
		/// </summary>
		protected List<TransportExceptionDescriptor> PublishExceptions { get; } = new();

		/// <summary>
		/// Resolves type descriptor
		/// </summary>
		public abstract void Resolve(object registration);

		/// <summary>
		/// Executes the message
		/// </summary>
		public abstract Task Execute(HorseClient client, HorseMessage message, object model);

		/// <summary>
		/// Interceptors before handler
		/// </summary>
		protected List<InterceptorDescriptor> BeforeInterceptors { get; } = new();

		/// <summary>
		/// Interceptors after handler
		/// </summary>
		protected List<InterceptorDescriptor> AfterInterceptors { get; } = new();

		/// <summary>
		/// Sends negative ack
		/// </summary>
		protected Task SendNegativeAck(HorseMessage message, HorseClient client, Exception exception)
		{
			string reason = NegativeReason switch
			{
				NegativeReason.Error            => HorseHeaders.NACK_REASON_ERROR,
				NegativeReason.ExceptionType    => exception.GetType().Name,
				NegativeReason.ExceptionMessage => exception.Message,
				_                               => HorseHeaders.NACK_REASON_NONE
			};

			return client.SendNegativeAck(message, reason);
		}

		/// <summary>
		/// Resolves base attributes
		/// </summary>
		protected void ResolveAttributes(Type type)
		{
			ResolveRetryAttribute(type);
			ResolveSendExceptionsAttributes(type);
			ResolveInterceptorAttributes(type);
		}

		private void ResolveRetryAttribute(MemberInfo type)
		{
			RetryAttribute retryAttr = type.GetCustomAttribute<RetryAttribute>();
			if (retryAttr != null)
				Retry = retryAttr;
		}

		#region SEND EXCEPTIONS

		private void ResolveSendExceptionsAttributes(MemberInfo type)
		{
			IEnumerable<PushExceptionsAttribute> pushAttributes = type.GetCustomAttributes<PushExceptionsAttribute>(true);
			foreach (PushExceptionsAttribute attribute in pushAttributes)
			{
				if (attribute.ExceptionType == null)
					DefaultPushException = new TransportExceptionDescriptor(attribute.ModelType);
				else
					PushExceptions.Add(new TransportExceptionDescriptor(attribute.ModelType, attribute.ExceptionType));
			}

			IEnumerable<PublishExceptionsAttribute> publishAttributes = type.GetCustomAttributes<PublishExceptionsAttribute>(true);
			foreach (PublishExceptionsAttribute attribute in publishAttributes)
			{
				if (attribute.ExceptionType == null)
					DefaultPublishException = new TransportExceptionDescriptor(attribute.ModelType);
				else
					PublishExceptions.Add(new TransportExceptionDescriptor(attribute.ModelType, attribute.ExceptionType));
			}
		}

		/// <summary>
		/// Sends exceptions to the server
		/// </summary>
		protected async Task SendExceptions(HorseMessage consumingMessage, HorseClient client, Exception exception)
		{
			if (PushExceptions.Count == 0 && PublishExceptions.Count == 0 && DefaultPushException == null && DefaultPublishException == null)
				return;

			Type type = exception.GetType();

			bool pushFound = false;
			foreach (TransportExceptionDescriptor item in PushExceptions)
			{
				if (!item.ExceptionType.IsAssignableFrom(type)) continue;
				await TransportToQueue(client, item, exception, consumingMessage);
				pushFound = true;
			}

			if (!pushFound && DefaultPushException != null)
				await TransportToQueue(client, DefaultPushException, exception, consumingMessage);

			bool publishFound = false;
			foreach (TransportExceptionDescriptor item in PublishExceptions)
			{
				if (!item.ExceptionType.IsAssignableFrom(type)) continue;
				await TransportToRouter(client, item, exception, consumingMessage);
				publishFound = true;
			}

			if (!publishFound && DefaultPublishException != null)
				await TransportToRouter(client, DefaultPublishException, exception, consumingMessage);
		}

		private Task TransportToQueue(HorseClient client, TransportExceptionDescriptor item, Exception exception, HorseMessage consumingMessage)
		{
			ITransportableException transportable = (ITransportableException) Activator.CreateInstance(item.ModelType);
			if (transportable == null)
				return Task.CompletedTask;

			transportable.Initialize(new ExceptionContext
			{
				Consumer = this,
				Exception = exception,
				ConsumingMessage = consumingMessage
			});

			return client.Queue.PushJson(transportable, false);
		}

		private Task TransportToRouter(HorseClient client, TransportExceptionDescriptor item, Exception exception, HorseMessage consumingMessage)
		{
			ITransportableException transportable = (ITransportableException) Activator.CreateInstance(item.ModelType);
			if (transportable == null)
				return Task.CompletedTask;

			transportable.Initialize(new ExceptionContext
			{
				Consumer = this,
				Exception = exception,
				ConsumingMessage = consumingMessage
			});

			return client.Router.PublishJson(transportable);
		}

		#endregion

		#region INTERCEPTORS

		private void ResolveInterceptorAttributes(Type type)
		{
			if (type.BaseType is not null) ResolveInterceptorAttributes(type.BaseType);
			IEnumerable<InterceptorAttribute> attrs = type.GetCustomAttributes<InterceptorAttribute>(false);
			foreach (InterceptorAttribute attr in attrs)
			{
				if (attr.Intercept == Intercept.Before)
					BeforeInterceptors.Add(new InterceptorDescriptor(attr.InterceptorType, attr.Intercept));
				else
					AfterInterceptors.Add(new InterceptorDescriptor(attr.InterceptorType, attr.Intercept));
			}
		}

		/// <summary>
		/// Run before interceptors
		/// </summary>
		/// <param name="message"></param>
		/// <param name="client"></param>
		/// <param name="handlerFactory"></param>
		protected async Task RunBeforeInterceptors(HorseMessage message, HorseClient client, IHandlerFactory handlerFactory = null)
		{
			if (BeforeInterceptors.Count == 0) return;
			List<IHorseInterceptor> interceptors = handlerFactory is null
				? BeforeInterceptors.Select(m => (IHorseInterceptor) Activator.CreateInstance(m.InterceptorType)).ToList()
				: BeforeInterceptors.Select(m => handlerFactory.CreateInterceptor(m.InterceptorType)).ToList();

			foreach (var interceptor in interceptors)
				await interceptor.Intercept(message, client);
		}

		/// <summary>
		/// Run after interceptors
		/// </summary>
		/// <param name="message"></param>
		/// <param name="client"></param>
		/// <param name="handlerFactory"></param>
		protected async Task RunAfterInterceptors(HorseMessage message, HorseClient client, IHandlerFactory handlerFactory = null)
		{
			if (AfterInterceptors.Count == 0) return;

			List<IHorseInterceptor> interceptors = handlerFactory is null
				? AfterInterceptors.Select(m => (IHorseInterceptor) Activator.CreateInstance(m.InterceptorType)).ToList()
				: AfterInterceptors.Select(m => handlerFactory.CreateInterceptor(m.InterceptorType)).ToList();

			foreach (var interceptor in interceptors)
				try
				{
					await interceptor.Intercept(message, client);
				}
				catch (Exception e)
				{
					client.OnException(e, message);
				}
		}

		#endregion
	}
}