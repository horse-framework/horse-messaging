using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Exceptions;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class ConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly IDirectConsumer<TModel> _directConsumer;
        private readonly IQueueConsumer<TModel> _queueConsumer;
        private bool _sendAck;
        private bool _sendNack;

        private KeyValuePair<string, ushort> _defaultPushException;
        private Dictionary<Type, KeyValuePair<string, ushort>> _pushExceptions;

        public ConsumerExecuter(IDirectConsumer<TModel> consumer)
        {
            _directConsumer = consumer;
            ResolveAttributes(consumer.GetType());
        }

        public ConsumerExecuter(IQueueConsumer<TModel> consumer)
        {
            _queueConsumer = consumer;
            ResolveAttributes(consumer.GetType());
        }

        private void ResolveAttributes(Type type)
        {
            AutoAckAttribute ackAttribute = type.GetCustomAttribute<AutoAckAttribute>();
            _sendAck = ackAttribute != null;

            AutoNackAttribute nackAttribute = type.GetCustomAttribute<AutoNackAttribute>();
            _sendNack = nackAttribute != null;

            _pushExceptions = new Dictionary<Type, KeyValuePair<string, ushort>>();
            IEnumerable<PushExceptionsAttribute> attributes = type.GetCustomAttributes<PushExceptionsAttribute>(false);
            foreach (PushExceptionsAttribute attribute in attributes)
            {
                if (attribute.ExceptionType == null)
                    _defaultPushException = new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId);
                else
                {
                    if (_pushExceptions.ContainsKey(attribute.ExceptionType))
                        throw new DuplicatePushException($"Multiple registration of {attribute.ExceptionType} for {typeof(TModel)}");

                    _pushExceptions.Add(attribute.ExceptionType, new KeyValuePair<string, ushort>(attribute.ChannelName, attribute.QueueId));
                }
            }
        }

        public override async Task Execute(TmqClient client, TmqMessage message, object model)
        {
            TModel t = (TModel) model;

            try
            {
                if (_queueConsumer != null)
                    await _queueConsumer.Consume(message, t, client);
                else if (_directConsumer != null)
                    await _directConsumer.Consume(message, t, client);
                else
                    throw new ArgumentNullException("There is no consumer defined");

                if (_sendAck)
                    await client.SendAck(message);
            }
            catch (Exception e)
            {
                if (_sendNack)
                    await client.SendNegativeAck(message, TmqHeaders.NACK_REASON_ERROR);

                Type exceptionType = e.GetType();
                var kv = _pushExceptions.ContainsKey(exceptionType)
                             ? _pushExceptions[exceptionType]
                             : _defaultPushException;

                if (!string.IsNullOrEmpty(kv.Key))
                {
                    string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(e);
                    await client.Queues.Push(kv.Key, kv.Value, serialized, false);
                }

                throw;
            }
        }
    }

    internal abstract class ConsumerExecuter
    {
        public abstract Task Execute(TmqClient client, TmqMessage message, object model);
    }
}