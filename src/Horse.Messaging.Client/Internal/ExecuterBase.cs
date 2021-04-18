using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Internal
{
    internal abstract class ExecuterBase
    {
        protected bool SendAck { get; private set; }
        protected bool SendNack { get; private set; }
        protected NackReason NackReason { get; private set; }
        
        protected RetryAttribute Retry { get; private set; }

        protected TransportExceptionDescriptor DefaultPushException { get; private set; }
        protected List<TransportExceptionDescriptor> PushExceptions { get; private set; }

        protected TransportExceptionDescriptor DefaultPublishException { get; private set; }
        protected List<TransportExceptionDescriptor> PublishExceptions { get; private set; }

        public abstract void Resolve(object registration);
        
        public abstract Task Execute(HorseClient client, HorseMessage message, object model);

        protected void ResolveAttributes(Type type)
        {
            if (!SendAck)
            {
                AutoAckAttribute ackAttribute = type.GetCustomAttribute<AutoAckAttribute>();
                SendAck = ackAttribute != null;
            }

            if (!SendNack)
            {
                AutoNackAttribute nackAttribute = type.GetCustomAttribute<AutoNackAttribute>();
                SendNack = nackAttribute != null;
                NackReason = nackAttribute != null ? nackAttribute.Reason : NackReason.None;
            }

            RetryAttribute retryAttr = type.GetCustomAttribute<RetryAttribute>();
            if (retryAttr != null)
                Retry = retryAttr;

            if (PushExceptions == null)
                PushExceptions = new List<TransportExceptionDescriptor>();

            IEnumerable<PushExceptionsAttribute> pushAttributes = type.GetCustomAttributes<PushExceptionsAttribute>(true);
            foreach (PushExceptionsAttribute attribute in pushAttributes)
            {
                if (attribute.ExceptionType == null)
                    DefaultPushException = new TransportExceptionDescriptor(attribute.ModelType);
                else
                    PushExceptions.Add(new TransportExceptionDescriptor(attribute.ModelType, attribute.ExceptionType));
            }

            if (PublishExceptions == null)
                PublishExceptions = new List<TransportExceptionDescriptor>();

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
        /// Sends negative ack
        /// </summary>
        protected Task SendNegativeAck(HorseMessage message, HorseClient client, Exception exception)
        {
            string reason;
            switch (NackReason)
            {
                case NackReason.Error:
                    reason = HorseHeaders.NACK_REASON_ERROR;
                    break;

                case NackReason.ExceptionType:
                    reason = exception.GetType().Name;
                    break;

                case NackReason.ExceptionMessage:
                    reason = exception.Message;
                    break;

                default:
                    reason = HorseHeaders.NACK_REASON_NONE;
                    break;
            }

            return client.SendNegativeAck(message, reason);
        }

        protected async Task SendExceptions(HorseMessage consumingMessage, HorseClient client, Exception exception)
        {
            if (PushExceptions.Count == 0 && PublishExceptions.Count == 0 && DefaultPushException == null && DefaultPublishException == null)
                return;

            Type type = exception.GetType();

            bool pushFound = false;
            foreach (TransportExceptionDescriptor item in PushExceptions)
            {
                if (item.ExceptionType.IsAssignableFrom(type))
                {
                    await TransportToQueue(client, item, exception, consumingMessage);
                    pushFound = true;
                }
            }

            if (!pushFound && DefaultPushException != null)
                await TransportToQueue(client, DefaultPushException, exception, consumingMessage);

            bool publishFound = false;
            foreach (TransportExceptionDescriptor item in PublishExceptions)
            {
                if (item.ExceptionType.IsAssignableFrom(type))
                {
                    await TransportToRouter(client, item, exception, consumingMessage);
                    publishFound = true;
                }
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

            return client.Queues.PushJson(transportable, false);
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

            return client.Routers.PublishJson(transportable);
        }
    }
}