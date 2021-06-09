using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Internal
{
    internal class QueueConsumerExecuter<TModel> : ConsumerExecuter
    {
        private readonly Type _consumerType;
        private readonly IQueueConsumer<TModel> _consumer;
        private readonly Func<IConsumerFactory> _consumerFactoryCreator;

        public QueueConsumerExecuter(Type consumerType, IQueueConsumer<TModel> consumer, Func<IConsumerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _consumer = consumer;
            _consumerFactoryCreator = consumerFactoryCreator;
        }

        public override void Resolve(ModelTypeConfigurator defaultOptions = null)
        {
            base.Resolve(defaultOptions);
            ResolveAttributes(_consumerType);
        }

        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            TModel t = (TModel) model;
            Exception exception = null;
            IConsumerFactory consumerFactory = null;

            try
            {
                if (_consumer != null)
                {
                    await RunBeforeInterceptors(message, client);
                    await Consume(_consumer, message, t, client);
                    await RunAfterInterceptors(message, client);
                }
                
                else if (_consumerFactoryCreator != null)
                {
                    consumerFactory = _consumerFactoryCreator();
                    consumerFactory.CreateScope();
                    object consumerObject = await consumerFactory.CreateConsumer(_consumerType);
                    IQueueConsumer<TModel> consumer = (IQueueConsumer<TModel>) consumerObject;
                    await RunBeforeInterceptors(message, client, consumerFactory);
                    await Consume(consumer, message, t, client);
                    await RunAfterInterceptors(message, client, consumerFactory);
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

                await SendExceptions(message, client, e);
                exception = e;
            }
            finally
            {
                if (consumerFactory != null)
                    consumerFactory.Consumed(exception);
            }
        }

        private async Task Consume(IQueueConsumer<TModel> consumer, HorseMessage message, TModel model, HorseClient client)
        {
            if (Retry == null)
            {
                await consumer.Consume(message, model, client);
                return;
            }

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    await consumer.Consume(message, model, client);
                    return;
                }
                catch (Exception e)
                {
                    Type type = e.GetType();
                    if (Retry.IgnoreExceptions != null && Retry.IgnoreExceptions.Length > 0)
                    {
                        if (Retry.IgnoreExceptions.Any(x => x.IsAssignableFrom(type)))
                            throw;
                    }

                    if (Retry.DelayBetweenRetries > 0)
                        await Task.Delay(Retry.DelayBetweenRetries);

                    if (i == count - 1)
                        throw;
                }
            }
        }
    }
}