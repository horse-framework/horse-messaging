using System.Reflection;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class ConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly IQueueConsumer<TModel> _consumer;
        private readonly bool _sendAck;
        private readonly bool _sendNack;

        public ConsumerExecuter(IQueueConsumer<TModel> consumer)
        {
            _consumer = consumer;

            AutoAckAttribute ackAttribute = consumer.GetType().GetCustomAttribute<AutoAckAttribute>();
            _sendAck = ackAttribute != null;

            AutoNackAttribute nackAttribute = consumer.GetType().GetCustomAttribute<AutoNackAttribute>();
            _sendNack = nackAttribute != null;
        }

        public override async Task Execute(TmqClient client, TmqMessage message, object model)
        {
            TModel t = (TModel) model;

            try
            {
                await _consumer.Consume(message, t);
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