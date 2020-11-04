using System;
using System.Collections.Generic;
using Twino.MQ.Client.Annotations;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client
{
    /// <summary>
    /// General options Configurator for model types.
    /// Model type attributes will overwrite the options defined with this configurator
    /// </summary>
    public class ModelTypeConfigurator
    {
        #region Push Properties

        internal List<Func<KeyValuePair<string, string>>> HeaderFactories { get; } = new List<Func<KeyValuePair<string, string>>>();
        internal Func<Type, string> QueueNameFactory { get; private set; }
        
        internal MessagingQueueStatus? QueueStatus { get; private set; }
        internal QueueAckDecision? AckDecision { get; private set; }
        internal int? DelayBetweenMessages { get; private set; }
        internal int? PutBackDelay { get; private set; }
        internal string Topic { get; private set; }

        #endregion

        #region Consumer Properties

        internal bool AutoAck { get; private set; }
        internal bool AutoNack { get; private set; }
        internal NackReason NackReason { get; private set; }

        internal RetryAttribute Retry { get; private set; }

        internal string DefaultPushException { get; private set; }
        internal List<Tuple<Type, string>> PushExceptions { get; } = new List<Tuple<Type, string>>();

        internal KeyValuePair<string, ushort> DefaultPublishException { get; private set; }
        internal List<Tuple<Type, KeyValuePair<string, ushort>>> PublishExceptions { get; } = new List<Tuple<Type, KeyValuePair<string, ushort>>>();

        #endregion

        #region Push Actions

        /// <summary>
        /// Sets Default queue name for each model type.
        /// Default value is model type name
        /// </summary>
        public ModelTypeConfigurator UseQueueName(Func<Type, string> func)
        {
            QueueNameFactory = func;
            return this;
        }

        /// <summary>
        /// Uses default topic value for all queues.
        /// Topic value is applied only queues are created with first push.
        /// These topic value can be overwritten with topic attribute.
        /// </summary>
        public ModelTypeConfigurator SetQueueTopic(string value)
        {
            Topic = value;
            return this;
        }

        /// <summary>
        /// Sets default queue status.
        /// Topic value is applied only queues are created with first push.
        /// That value can be overwritten with queue status attribute.
        /// </summary>
        public ModelTypeConfigurator SetQueueStatus(MessagingQueueStatus value)
        {
            QueueStatus = value;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public ModelTypeConfigurator SetQueueAcknowledge(QueueAckDecision value)
        {
            AckDecision = value;
            return this;
        }

        /// <summary>
        /// Uses default delay between messages for queue models
        /// </summary>
        public ModelTypeConfigurator SetDelayBetweenMessages(TimeSpan value)
        {
            DelayBetweenMessages = Convert.ToInt32(value.TotalMilliseconds);
            return this;
        }

        /// <summary>
        /// Sets default put back delay value
        /// </summary>
        public ModelTypeConfigurator SetPutBackDelay(TimeSpan value)
        {
            PutBackDelay = Convert.ToInt32(value.TotalMilliseconds);
            return this;
        }

        /// <summary>
        /// Sets default delivery handler name for auto created queues
        /// </summary>
        public ModelTypeConfigurator UseDeliveryHandler(string handlerName)
        {
            return AddMessageHeader(TwinoHeaders.DELIVERY_HANDLER, handlerName);
        }

        /// <summary>
        /// Adds message header to all messages that are pushed or published with bus
        /// </summary>
        public ModelTypeConfigurator AddMessageHeader(string key, string value)
        {
            HeaderFactories.Add(() => new KeyValuePair<string, string>(key, value));
            return this;
        }

        /// <summary>
        /// Adds message header to all messages that are pushed or published with bus
        /// </summary>
        public ModelTypeConfigurator AddMessageHeader(Func<KeyValuePair<string, string>> func)
        {
            HeaderFactories.Add(func);
            return this;
        }

        #endregion

        #region Consumer Actions

        /// <summary>
        /// Sets as default value, consumers send acknowledge if consume operation succeded  
        /// </summary>
        public ModelTypeConfigurator UseConsumerAck()
        {
            AutoAck = true;
            return this;
        }

        /// <summary>
        /// Sets as default value, consumers send negative acknowledge if consume operation fails
        /// </summary>
        public ModelTypeConfigurator UseConsumerNack(NackReason reason)
        {
            AutoNack = true;
            NackReason = reason;
            return this;
        }

        /// <summary>
        /// Applies retry policy to all consumers
        /// </summary>
        public ModelTypeConfigurator UseRetry(int count, int delayBetweenRetries = 50, params Type[] ignoreExceptions)
        {
            Retry = new RetryAttribute(count, delayBetweenRetries, ignoreExceptions);
            return this;
        }

        /// <summary>
        /// Push exceptions in consumers' consume operations to specified queue
        /// </summary>
        public ModelTypeConfigurator PushConsumerExceptions(string queueName)
        {
            DefaultPushException = queueName;
            return this;
        }

        /// <summary>
        /// Push exceptions in specified types in consumers' consume operations to specified queue
        /// </summary>
        public ModelTypeConfigurator PushConsumerExceptions(Type exceptionType, string queueName)
        {
            PushExceptions.Add(new Tuple<Type, string>(exceptionType, queueName));
            return this;
        }

        /// <summary>
        /// Publish exceptions in consumers' consume operations to specified route
        /// </summary>
        public ModelTypeConfigurator PublishConsumerExceptions(string routerName, ushort contentType = 0)
        {
            DefaultPublishException = new KeyValuePair<string, ushort>(routerName, contentType);
            return this;
        }

        /// <summary>
        /// Publish exceptions in specified types in consumers' consume operations to specified route
        /// </summary>
        public ModelTypeConfigurator PublishConsumerExceptions(Type exceptionType, string routerName, ushort contentType = 0)
        {
            PublishExceptions.Add(new Tuple<Type, KeyValuePair<string, ushort>>(exceptionType, new KeyValuePair<string, ushort>(routerName, contentType)));
            return this;
        }

        #endregion
    }
}