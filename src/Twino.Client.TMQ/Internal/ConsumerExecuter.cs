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
        protected KeyValuePair<string, ushort> DefaultPushException { get; private set; }
        protected Dictionary<Type, KeyValuePair<string, ushort>> PushExceptions { get; private set; }

        public abstract Task Execute(TmqClient client, TmqMessage message, object model);

        protected void ResolveAttributes(Type type, Type modelType)
        {
            AutoAckAttribute ackAttribute = type.GetCustomAttribute<AutoAckAttribute>();
            SendAck = ackAttribute != null;

            AutoNackAttribute nackAttribute = type.GetCustomAttribute<AutoNackAttribute>();
            SendNack = nackAttribute != null;

            PushExceptions = new Dictionary<Type, KeyValuePair<string, ushort>>();
            IEnumerable<PushExceptionsAttribute> attributes = type.GetCustomAttributes<PushExceptionsAttribute>(false);
            foreach (PushExceptionsAttribute attribute in attributes)
            {
                if (attribute.ExceptionType == null)
                    DefaultPushException = new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId);
                else
                {
                    if (PushExceptions.ContainsKey(attribute.ExceptionType))
                        throw new DuplicatePushException($"Multiple registration of {attribute.ExceptionType} for {modelType}");

                    PushExceptions.Add(attribute.ExceptionType, new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId));
                }
            }
        }
    }
}