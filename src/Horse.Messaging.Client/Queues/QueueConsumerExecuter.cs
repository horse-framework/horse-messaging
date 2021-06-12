using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues
{
    internal class QueueConsumerExecuter<TModel> : ExecuterBase
    {
        private readonly Type _consumerType;
        private readonly IQueueConsumer<TModel> _consumer;
        private readonly Func<IHandlerFactory> _consumerFactoryCreator;

        public QueueConsumerExecuter(Type consumerType, IQueueConsumer<TModel> consumer, Func<IHandlerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _consumer = consumer;
            _consumerFactoryCreator = consumerFactoryCreator;
        }

        public override void Resolve(object registration)
        {
            QueueConsumerRegistration reg = registration as QueueConsumerRegistration;
            // TODO : Emre | reg null olma ihtimali var mı? 
            ResolveAttributes(reg!.ConsumerType);
            ResolveQueueAttributes();
        }

        private void ResolveQueueAttributes()
        {
            if (!SendPositiveResponse)
            {
                AutoAckAttribute ackAttribute = _consumerType.GetCustomAttribute<AutoAckAttribute>();
                SendPositiveResponse = ackAttribute != null;
            }

            if (!SendNegativeResponse)
            {
                AutoNackAttribute nackAttribute = _consumerType.GetCustomAttribute<AutoNackAttribute>();
                SendNegativeResponse = nackAttribute != null;
                NegativeReason = nackAttribute?.Reason ?? NegativeReason.None;
            }
        }

        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            TModel t = (TModel) model;
            ProvidedHandler providedHandler = null;

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
                    IHandlerFactory handlerFactory = _consumerFactoryCreator();
                    providedHandler = handlerFactory.CreateHandler(_consumerType);
                    IQueueConsumer<TModel> consumer = (IQueueConsumer<TModel>) providedHandler.Service;
                    await RunBeforeInterceptors(message, client, handlerFactory);
                    await Consume(consumer, message, t, client);
                    await RunAfterInterceptors(message, client, handlerFactory);
                }
                else
                    throw new NullReferenceException("There is no consumer defined");

                if (SendPositiveResponse)
                    await client.SendAck(message);
            }
            catch (Exception e)
            {
                if (SendNegativeResponse)
                    await SendNegativeAck(message, client, e);

                await SendExceptions(message, client, e);
            }
            finally
            {
                providedHandler?.Dispose();
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
                    if (Retry.IgnoreExceptions is { Length: > 0 })
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