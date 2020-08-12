using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Exceptions;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal abstract class ConsumerExecuter
    {
        protected bool SendAck { get; private set; }
        protected bool SendNack { get; private set; }
        protected NackReason NackReason { get; private set; }

        protected KeyValuePair<string, ushort> DefaultPushException { get; private set; }
        protected List<Tuple<Type, KeyValuePair<string, ushort>>> PushExceptions { get; private set; }

        protected KeyValuePair<string, ushort> DefaultPublishException { get; private set; }
        protected List<Tuple<Type, KeyValuePair<string, ushort>>> PublishExceptions { get; private set; }

        public abstract Task Execute(TmqClient client, TmqMessage message, object model);

        protected void ResolveAttributes(Type type, Type modelType)
        {
            AutoAckAttribute ackAttribute = type.GetCustomAttribute<AutoAckAttribute>();
            SendAck = ackAttribute != null;

            AutoNackAttribute nackAttribute = type.GetCustomAttribute<AutoNackAttribute>();
            SendNack = nackAttribute != null;
            NackReason = nackAttribute != null ? nackAttribute.Reason : NackReason.None;

            PushExceptions = new List<Tuple<Type, KeyValuePair<string, ushort>>>();
            IEnumerable<PushExceptionsAttribute> pushAttributes = type.GetCustomAttributes<PushExceptionsAttribute>(true);
            foreach (PushExceptionsAttribute attribute in pushAttributes)
            {
                if (attribute.ExceptionType == null)
                    DefaultPushException = new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId);
                else
                    PushExceptions.Add(new Tuple<Type, KeyValuePair<string, ushort>>(attribute.ExceptionType,
                                                                                     new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId)));
            }

            PublishExceptions = new List<Tuple<Type, KeyValuePair<string, ushort>>>();
            IEnumerable<PublishExceptionsAttribute> publishAttributes = type.GetCustomAttributes<PublishExceptionsAttribute>(true);
            foreach (PublishExceptionsAttribute attribute in publishAttributes)
            {
                if (attribute.ExceptionType == null)
                    DefaultPublishException = new KeyValuePair<string, ushort>(attribute.RouterName, attribute.ContentType);
                else
                    PublishExceptions.Add(new Tuple<Type, KeyValuePair<string, ushort>>(attribute.ExceptionType,
                                                                                        new KeyValuePair<string, ushort>(attribute.RouterName, attribute.ContentType)));
            }
        }

        /// <summary>
        /// Sends negative ack
        /// </summary>
        protected Task SendNegativeAck(TmqMessage message, TmqClient client, Exception exception)
        {
            string reason;
            switch (NackReason)
            {
                case NackReason.Error:
                    reason = TmqHeaders.NACK_REASON_ERROR;
                    break;

                case NackReason.ExceptionType:
                    reason = exception.GetType().Name;
                    break;

                case NackReason.ExceptionMessage:
                    reason = exception.Message;
                    break;

                default:
                    reason = TmqHeaders.NACK_REASON_NONE;
                    break;
            }

            return client.SendNegativeAck(message, reason);
        }

        protected async Task SendExceptions(TmqClient client, Exception exception)
        {
            if (PushExceptions.Count == 0 &&
                PublishExceptions.Count == 0 &&
                string.IsNullOrEmpty(DefaultPushException.Key) &&
                string.IsNullOrEmpty(DefaultPublishException.Key))
                return;

            Type type = exception.GetType();
            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(exception);

            bool pushFound = false;
            foreach (Tuple<Type, KeyValuePair<string, ushort>> tuple in PushExceptions)
            {
                if (tuple.Item1.IsAssignableFrom(type))
                {
                    await client.Queues.Push(tuple.Item2.Key, tuple.Item2.Value, serialized, false);
                    pushFound = true;
                }
            }

            if (!pushFound && !string.IsNullOrEmpty(DefaultPushException.Key))
                await client.Queues.Push(DefaultPushException.Key, DefaultPushException.Value, serialized, false);

            bool publishFound = false;
            foreach (Tuple<Type, KeyValuePair<string, ushort>> tuple in PublishExceptions)
            {
                if (tuple.Item1.IsAssignableFrom(type))
                {
                    await client.Routers.Publish(tuple.Item2.Key, serialized, false, tuple.Item2.Value);
                    publishFound = true;
                }
            }

            if (!publishFound && !string.IsNullOrEmpty(DefaultPublishException.Key))
                await client.Routers.Publish(DefaultPublishException.Key, serialized, false, DefaultPublishException.Value);
        }
    }
}