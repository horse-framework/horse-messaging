using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Internal
{
    internal abstract class ConsumerExecuter
    {
        #region Properties

        protected bool SendAck { get; private set; }
        protected bool SendNack { get; private set; }
        protected NackReason NackReason { get; private set; }

        protected RetryAttribute Retry { get; private set; }

        protected TransportExceptionDescriptor DefaultPushException { get; private set; }
        protected List<TransportExceptionDescriptor> PushExceptions { get; private set; }

        protected TransportExceptionDescriptor DefaultPublishException { get; private set; }
        protected List<TransportExceptionDescriptor> PublishExceptions { get; private set; }

        #endregion

        #region Actions

        public virtual void Resolve(ModelTypeConfigurator defaultOptions = null)
        {
            if (defaultOptions == null)
                return;

            SendAck = defaultOptions.AutoAck;
            SendNack = defaultOptions.AutoNack;
            NackReason = defaultOptions.NackReason;
            Retry = defaultOptions.Retry;
            DefaultPublishException = defaultOptions.DefaultPublishException;
            PushExceptions = defaultOptions.PushExceptions;
            DefaultPublishException = defaultOptions.DefaultPublishException;
            PublishExceptions = defaultOptions.PublishExceptions;

            if (DefaultPushException != null && !typeof(ITransportableException).IsAssignableFrom(DefaultPushException.ModelType))
                throw new InvalidCastException("PushException model type must implement ITransportableException interface");

            foreach (TransportExceptionDescriptor item in PushExceptions)
                if (item != null && !typeof(ITransportableException).IsAssignableFrom(item.ModelType))
                    throw new InvalidCastException("PushException model type must implement ITransportableException interface");

            if (DefaultPublishException != null && !typeof(ITransportableException).IsAssignableFrom(DefaultPublishException.ModelType))
                throw new InvalidCastException("PublishException model type must implement ITransportableException interface");

            foreach (TransportExceptionDescriptor item in PublishExceptions)
                if (item != null && !typeof(ITransportableException).IsAssignableFrom(item.ModelType))
                    throw new InvalidCastException("PublishException model type must implement ITransportableException interface");
        }

        public abstract Task Execute(TmqClient client, TwinoMessage message, object model);

        protected void ResolveAttributes(Type type, Type modelType)
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
        protected Task SendNegativeAck(TwinoMessage message, TmqClient client, Exception exception)
        {
            string reason;
            switch (NackReason)
            {
                case NackReason.Error:
                    reason = TwinoHeaders.NACK_REASON_ERROR;
                    break;

                case NackReason.ExceptionType:
                    reason = exception.GetType().Name;
                    break;

                case NackReason.ExceptionMessage:
                    reason = exception.Message;
                    break;

                default:
                    reason = TwinoHeaders.NACK_REASON_NONE;
                    break;
            }

            return client.SendNegativeAck(message, reason);
        }

        protected async Task SendExceptions(TwinoMessage consumingMessage, TmqClient client, Exception exception)
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

        private async Task TransportToQueue(TmqClient client, TransportExceptionDescriptor item, Exception exception, TwinoMessage consumingMessage)
        {
            ITransportableException transportable = (ITransportableException) Activator.CreateInstance(item.ModelType);
            if (transportable == null)
                return;

            transportable.Initialize(new ExceptionContext
                                     {
                                         Consumer = this,
                                         Exception = exception,
                                         ConsumingMessage = consumingMessage
                                     });

            await client.Queues.PushJson(transportable, false);
        }

        private async Task TransportToRouter(TmqClient client, TransportExceptionDescriptor item, Exception exception, TwinoMessage consumingMessage)
        {
            ITransportableException transportable = (ITransportableException) Activator.CreateInstance(item.ModelType);
            if (transportable == null)
                return;

            transportable.Initialize(new ExceptionContext
                                     {
                                         Consumer = this,
                                         Exception = exception,
                                         ConsumingMessage = consumingMessage
                                     });

            await client.Routers.PublishJson(transportable);
        }

        #endregion
    }
}