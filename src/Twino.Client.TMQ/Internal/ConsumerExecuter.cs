using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Exceptions;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal enum ConsumerMethod
    {
        Queue,
        Direct
    }

    internal class ConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly Func<IConsumerFactory> _consumerFactoryCreator;

        private bool _sendAck;
        private bool _sendNack;
        private readonly ConsumerMethod _method;
        private readonly IQueueConsumer<TModel> _queueConsumer;
        private readonly IDirectConsumer<TModel> _directConsumer;

        private readonly Type _consumerType;

        private KeyValuePair<string, ushort> _defaultPushException;
        private Dictionary<Type, KeyValuePair<string, ushort>> _pushExceptions;

        public ConsumerExecuter(Type consumerType, ConsumerMethod method, object consumer, Func<IConsumerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _method = method;

            if (consumer != null)
            {
                if (_method == ConsumerMethod.Queue)
                    _queueConsumer = (IQueueConsumer<TModel>) consumer;
                else
                    _directConsumer = (IDirectConsumer<TModel>) consumer;
            }

            _consumerFactoryCreator = consumerFactoryCreator;
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
                if (_method == ConsumerMethod.Queue)
                {
                    if (_queueConsumer != null)
                        await _queueConsumer.Consume(message, t, client);
                    else if (_consumerFactoryCreator != null)
                    {
                        consumerFactory = _consumerFactoryCreator();
                        object consumerObject = await consumerFactory.CreateConsumer(_consumerType);
                        IQueueConsumer<TModel> consumer = (IQueueConsumer<TModel>) consumerObject;
                        await consumer.Consume(message, t, client);
                    }
                    else
                        throw new ArgumentNullException("There is no consumer defined");
                }
                else
                {
                    if (_directConsumer != null)
                        await _directConsumer.Consume(message, t, client);
                    else if (_consumerFactoryCreator != null)
                    {
                        consumerFactory = _consumerFactoryCreator();
                        object consumerObject = await consumerFactory.CreateConsumer(_consumerType);
                        IDirectConsumer<TModel> consumer = (IDirectConsumer<TModel>) consumerObject;
                        await consumer.Consume(message, t, client);
                    }
                    else
                        throw new ArgumentNullException("There is no consumer defined");
                }

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