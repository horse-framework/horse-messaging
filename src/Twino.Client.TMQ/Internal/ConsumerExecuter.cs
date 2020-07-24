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
        private readonly Func<IConsumerFactory> _consumerFactoryCreator;
#pragma warning disable 618
        private readonly ITwinoConsumer<TModel> _consumer;
#pragma warning restore 618
        private bool _sendAck;
        private bool _sendNack;

        private readonly Type _consumerType;

        private KeyValuePair<string, ushort> _defaultPushException;
        private Dictionary<Type, KeyValuePair<string, ushort>> _pushExceptions;

#pragma warning disable 618
        public ConsumerExecuter(Type consumerType, ITwinoConsumer<TModel> consumer, Func<IConsumerFactory> consumerFactoryCreator)
#pragma warning restore 618
        {
            _consumer = consumer;
            _consumerFactoryCreator = consumerFactoryCreator;
            _consumerType = consumerType;
            ResolveAttributes(consumerType);
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
            Exception exception = null;
            IConsumerFactory consumerFactory = null;

            try
            {
                if (_consumer != null)
                    await _consumer.Consume(message, t, client);
                else if (_consumerFactoryCreator != null)
                {
                    consumerFactory = _consumerFactoryCreator();
                    object consumerObject = await consumerFactory.CreateConsumer(_consumerType);
#pragma warning disable 618
                    ITwinoConsumer<TModel> consumer = (ITwinoConsumer<TModel>) consumerObject;
#pragma warning restore 618
                    await consumer.Consume(message, t, client);
                }
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

                exception = e;
                throw;
            }
            finally
            {
                if (consumerFactory != null)
                    consumerFactory.Consumed(exception);
            }
        }
    }

    internal abstract class ConsumerExecuter
    {
        public abstract Task Execute(TmqClient client, TmqMessage message, object model);
    }
}