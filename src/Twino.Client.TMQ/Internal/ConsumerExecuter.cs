using System;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class ConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly IDirectConsumer<TModel> _directConsumer;
        private readonly IQueueConsumer<TModel> _queueConsumer;
        private bool _sendAck;
        private bool _sendNack;

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
        }

        public override async Task Execute(TmqClient client, TmqMessage message, object model)
        {
            TModel t = (TModel) model;

            try
            {
                if (_queueConsumer != null)
                    await _queueConsumer.Consume(message, t);
                else if (_directConsumer != null)
                    await _directConsumer.Consume(message, t);
                else
                    throw new ArgumentNullException("There is no consumer defined");

                if (_sendAck)
                    await client.SendAck(message);
            }
            catch
            {
                if (_sendNack)
                    await client.SendNegativeAck(message, TmqHeaders.NACK_REASON_ERROR);

                //throw for message consumer event
                throw;
            }
        }
    }

    internal abstract class ConsumerExecuter
    {
        public abstract Task Execute(TmqClient client, TmqMessage message, object model);
    }
}