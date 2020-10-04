using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class DirectConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly Type _consumerType;
        private readonly IDirectConsumer<TModel> _consumer;
        private readonly Func<IConsumerFactory> _consumerFactoryCreator;

        public DirectConsumerExecuter(Type consumerType, IDirectConsumer<TModel> consumer, Func<IConsumerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _consumer = consumer;
            _consumerFactoryCreator = consumerFactoryCreator;
            ResolveAttributes(consumerType, typeof(TModel));
        }

        public override async Task Execute(TmqClient client, TwinoMessage message, object model)
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
                    IDirectConsumer<TModel> consumer = (IDirectConsumer<TModel>) consumerObject;
                    await consumer.Consume(message, t, client);
                }
                else
                    throw new ArgumentNullException("There is no consumer defined");


                if (SendAck)
                    await client.SendAck(message);
            }
            catch (Exception e)
            {
                if (SendNack)
                    await SendNegativeAck(message, client, e);

                await SendExceptions(client, e);
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
}