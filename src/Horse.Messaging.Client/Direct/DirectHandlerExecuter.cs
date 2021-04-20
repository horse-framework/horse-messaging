using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    internal class DirectHandlerExecuter<TModel> : ExecuterBase
    {
        private readonly Type _consumerType;
        private readonly IDirectMessageHandler<TModel> _messageHandler;
        private readonly Func<IHandlerFactory> _consumerFactoryCreator;
        private DirectHandlerRegistration _registration;

        public DirectHandlerExecuter(Type consumerType, IDirectMessageHandler<TModel> messageHandler, Func<IHandlerFactory> consumerFactoryCreator)
        {
            _consumerType = consumerType;
            _messageHandler = messageHandler;
            _consumerFactoryCreator = consumerFactoryCreator;
        }

        public override void Resolve(object registration)
        {
            DirectHandlerRegistration reg = registration as DirectHandlerRegistration;
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
            ProvidedHandler providedHandler = null;

            try
            {
                if (_messageHandler != null)
                    await Consume(_messageHandler, message, t, client);

                else if (_consumerFactoryCreator != null)
                {
                    IHandlerFactory handlerFactory = _consumerFactoryCreator();
                    providedHandler = handlerFactory.CreateHandler(_consumerType);
                    IDirectMessageHandler<TModel> messageHandler = (IDirectMessageHandler<TModel>) providedHandler.Service;
                    await Consume(messageHandler, message, t, client);
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
            }
            finally
            {
                if (providedHandler != null)
                    providedHandler.Dispose();
            }
        }

        private async Task Consume(IDirectMessageHandler<TModel> messageHandler, HorseMessage message, TModel model, HorseClient client)
        {
            if (Retry == null)
            {
                await messageHandler.Consume(message, model, client);
                return;
            }

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    await messageHandler.Consume(message, model, client);
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