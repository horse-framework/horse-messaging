using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Channel subscriber executor
/// </summary>
internal class ChannelSubscriberExecutor<TModel> : ExecutorBase
{
    private readonly Type _subscriberType;
    private readonly IChannelSubscriber<TModel> _subscriber;
    private readonly Func<IHandlerFactory> _subscriberFactoryCreator;

    /// <summary>
    /// Channel subscriber executor
    /// </summary>
    public ChannelSubscriberExecutor(Type subscriberType, IChannelSubscriber<TModel> subscriber, Func<IHandlerFactory> subscriberFactoryCreator)
    {
        _subscriberType = subscriberType;
        _subscriber = subscriber;
        _subscriberFactoryCreator = subscriberFactoryCreator;
    }

    /// <summary>
    /// Resolves channel subscriber executor types
    /// </summary>
    public override void Resolve(object registration)
    {
        ChannelSubscriberRegistration reg = registration as ChannelSubscriberRegistration;
        ResolveAttributes(reg!.SubscriberType);
    }

    /// <summary>
    /// Executes the channel message
    /// </summary>
    public override async Task Execute(HorseClient client, HorseMessage message, object model,
        CancellationToken cancellationToken)
    {
        TModel t = (TModel)model;
        ProvidedHandler providedHandler = null;
        var context = new ChannelMessageContext<TModel>(message, t, client, cancellationToken);
        IChannelSubscriber<TModel> consumer = null;

        try
        {
            if (_subscriber != null)
                await Handle(_subscriber, context);

            else if (_subscriberFactoryCreator != null)
            {
                IHandlerFactory handlerFactory = _subscriberFactoryCreator();
                providedHandler = handlerFactory.CreateHandler(_subscriberType);
                consumer = (IChannelSubscriber<TModel>)providedHandler.Service;
                await Handle(consumer, context);
            }
            else
                throw new NullReferenceException("There is no consumer defined");
        }
        catch (Exception e)
        {
            await SendExceptions(message, client, e);

            try
            {
                if (_subscriber != null)
                    await _subscriber.Error(e, context);
                else if (consumer != null)
                    await consumer.Error(e, context);
            }
            catch
            {
                //hide impl exceptions
            }
        }
        finally
        {
            providedHandler?.Dispose();
        }
    }

    private async Task Handle(IChannelSubscriber<TModel> subscriber, ChannelMessageContext<TModel> context)
    {
        if (Retry == null)
        {
            await subscriber.Handle(context);
            return;
        }

        int count = Retry.Count == 0 ? 100 : Retry.Count;
        for (int i = 0; i < count; i++)
        {
            try
            {
                context.TryCount = i;
                await subscriber.Handle(context);
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
                    await Task.Delay(Retry.DelayBetweenRetries, context.CancellationToken);

                if (i == count - 1)
                    throw;
            }
        }
    }
}