using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events;

internal class EventSubscriberExecutor : ExecutorBase
{
    private readonly Type _handlerType;
    private readonly IHorseEventHandler _handler;
    private readonly Func<IHandlerFactory> _subscriberFactoryCreator;
    private EventSubscriberRegistration _registration;

    /// <summary>
    /// Channel subscriber executor
    /// </summary>
    public EventSubscriberExecutor(Type handlerType, IHorseEventHandler handler, Func<IHandlerFactory> handlerFactoryCreator)
    {
        _handlerType = handlerType;
        _handler = handler;
        _subscriberFactoryCreator = handlerFactoryCreator;
    }

    public override void Resolve(object registration)
    {
        EventSubscriberRegistration reg = registration as EventSubscriberRegistration;
        _registration = reg;
    }

    public override async Task Execute(HorseClient client, HorseMessage message, object model)
    {
        HorseEvent horseEvent = (HorseEvent) model;
        ProvidedHandler providedHandler = null;

        try
        {
            if (_handler != null)
                await Handle(_handler, message, horseEvent, client);

            else if (_subscriberFactoryCreator != null)
            {
                IHandlerFactory handlerFactory = _subscriberFactoryCreator();
                providedHandler = handlerFactory.CreateHandler(_handlerType);
                IHorseEventHandler consumer = (IHorseEventHandler) providedHandler.Service;
                await Handle(consumer, message, horseEvent, client);
            }
            else
                throw new ArgumentNullException("There is no event handler");
        }
        catch (Exception e)
        {
            await SendExceptions(message, client, e);
        }
        finally
        {
            if (providedHandler != null)
                providedHandler.Dispose();
        }
    }

    private async Task Handle(IHorseEventHandler handler, HorseMessage message, HorseEvent horseEvent, HorseClient client)
    {
        if (Retry == null)
        {
            await handler.Handle(horseEvent, client);
            return;
        }

        int count = Retry.Count == 0 ? 100 : Retry.Count;
        for (int i = 0; i < count; i++)
        {
            try
            {
                await handler.Handle(horseEvent, client);
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