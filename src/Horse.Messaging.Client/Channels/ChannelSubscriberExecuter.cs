using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Channel subscriber executer
    /// </summary>
    internal class ChannelSubscriberExecuter<TModel> : ExecuterBase
    {
        private readonly Type _subscriberType;
        private readonly IChannelSubscriber<TModel> _subscriber;
        private readonly Func<IHandlerFactory> _subscriberFactoryCreator;

        /// <summary>
        /// Channel subscriber executer
        /// </summary>
        public ChannelSubscriberExecuter(Type subscriberType, IChannelSubscriber<TModel> subscriber, Func<IHandlerFactory> subscriberFactoryCreator)
        {
            _subscriberType = subscriberType;
            _subscriber = subscriber;
            _subscriberFactoryCreator = subscriberFactoryCreator;
        }

        /// <summary>
        /// Resolves channel subscriber executer types
        /// </summary>
        public override void Resolve(object registration)
        {
            ChannelSubscriberRegistration reg = registration as ChannelSubscriberRegistration;
            ResolveAttributes(reg!.SubscriberType);
        }

        /// <summary>
        /// Executes the channel message
        /// </summary>
        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            TModel t = (TModel) model;
            ProvidedHandler providedHandler = null;

            try
            {
                if (_subscriber != null)
                    await Handle(_subscriber, message, t, client);

                else if (_subscriberFactoryCreator != null)
                {
                    IHandlerFactory handlerFactory = _subscriberFactoryCreator();
                    providedHandler = handlerFactory.CreateHandler(_subscriberType);
                    IChannelSubscriber<TModel> consumer = (IChannelSubscriber<TModel>) providedHandler.Service;
                    await Handle(consumer, message, t, client);
                }
                else
                    throw new NullReferenceException("There is no consumer defined");
            }
            catch (Exception e)
            {
                await SendExceptions(message, client, e);
            }
            finally
            {
                providedHandler?.Dispose();
            }
        }

        private async Task Handle(IChannelSubscriber<TModel> subscriber, HorseMessage message, TModel model, HorseClient client)
        {
            if (Retry == null)
            {
                await subscriber.Handle(model, message, client);
                return;
            }

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    await subscriber.Handle(model, message, client);
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