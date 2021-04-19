using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    internal class DirectConsumerExecuter<TModel> : ExecuterBase
    {
        private readonly Type _consumerType;
        private readonly IDirectMessageReceiver<TModel> _messageReceiver;
        private readonly Func<IConsumerFactory> _consumerFactoryCreator;
        private DirectConsumeRegistration _registration;

        public DirectConsumerExecuter(Type consumerType, IDirectMessageReceiver<TModel> messageReceiver, Func<IConsumerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _messageReceiver = messageReceiver;
            _consumerFactoryCreator = consumerFactoryCreator;
        }

        public override void Resolve(object registration)
        {
            DirectConsumeRegistration reg = registration as DirectConsumeRegistration;
            _registration = reg;
            
            ResolveAttributes(reg.ConsumerType);
            ResolveDirectAttributes();
        }

        private void ResolveDirectAttributes()
        {
            //todo: resolve consume attributes
        }

        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            TModel t = (TModel) model;
            Exception exception = null;
            IConsumerFactory consumerFactory = null;

            try
            {
                if (_messageReceiver != null)
                    await Consume(_messageReceiver, message, t, client);

                else if (_consumerFactoryCreator != null)
                {
                    consumerFactory = _consumerFactoryCreator();
                    object consumerObject = await consumerFactory.CreateConsumer(_consumerType);
                    IDirectMessageReceiver<TModel> messageReceiver = (IDirectMessageReceiver<TModel>) consumerObject;
                    await Consume(messageReceiver, message, t, client);
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

        private async Task Consume(IDirectMessageReceiver<TModel> messageReceiver, HorseMessage message, TModel model, HorseClient client)
        {
            if (Retry == null)
            {
                await messageReceiver.Consume(message, model, client);
                return;
            }

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    await messageReceiver.Consume(message, model, client);
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
                }
            }
        }
    }
}