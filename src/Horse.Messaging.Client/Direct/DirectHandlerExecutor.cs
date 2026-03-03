using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct;

internal class DirectHandlerExecutor<TModel> : ExecutorBase
{
    private readonly Type _consumerType;
    private readonly IDirectMessageHandler<TModel> _messageHandler;
    private readonly Func<IHandlerFactory> _consumerFactoryCreator;
    private DirectHandlerRegistration _registration;
    private InterceptorRunner _interceptorRunner;
    
    public DirectHandlerExecutor(Type consumerType, IDirectMessageHandler<TModel> messageHandler, Func<IHandlerFactory> consumerFactoryCreator)
    {
        _consumerType = consumerType;
        _messageHandler = messageHandler;
        _consumerFactoryCreator = consumerFactoryCreator;
    }

    public override void Resolve(object registration)
    {
        _registration = registration as DirectHandlerRegistration;
        _interceptorRunner = new InterceptorRunner(_registration!.IntercetorDescriptors);
        ResolveAttributes(_registration!.HandlerType);
        ResolveDirectAttributes();
    }

    private void ResolveDirectAttributes()
    {
        AutoResponseAttribute responseAttribute = _consumerType.GetCustomAttribute<AutoResponseAttribute>();
        if (responseAttribute == null)
            return;
        if (responseAttribute.Response is AutoResponse.All or AutoResponse.OnSuccess)
            SendPositiveResponse = true;
        if (responseAttribute.Response is not (AutoResponse.All or AutoResponse.OnError))
            return;

        SendNegativeResponse = true;
        NegativeReason = responseAttribute.Error;
    }

    public override async Task Execute(HorseClient client, HorseMessage message, object model,
        CancellationToken cancellationToken = default)
    {
        TModel t = (TModel) model;
        ProvidedHandler providedHandler = null;

        try
        {
            if (_messageHandler != null)
            {
                await Handle(_messageHandler, message, t, client, cancellationToken: cancellationToken);
                
            }
            else if (_consumerFactoryCreator != null)
            {
                IHandlerFactory handlerFactory = _consumerFactoryCreator();
                providedHandler = handlerFactory.CreateHandler(_consumerType);
                IDirectMessageHandler<TModel> messageHandler = (IDirectMessageHandler<TModel>) providedHandler.Service;
                await Handle(messageHandler, message, t, client, handlerFactory, cancellationToken);
            }
            else
                throw new InvalidOperationException("There is no handler defined");

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

    private async Task Handle(IDirectMessageHandler<TModel> messageHandler, HorseMessage message, TModel model,
        HorseClient client, IHandlerFactory handlerFactory = null, CancellationToken cancellationToken = default)
    {
        if (Retry == null)
        {
            await _interceptorRunner.RunBeforeInterceptors(message, client, cancellationToken: cancellationToken);
            await messageHandler.Handle(message, model, client, cancellationToken);
            await _interceptorRunner.RunAfterInterceptors(message, client, cancellationToken: cancellationToken);
            return;
        }

        int count = Retry.Count == 0 ? 100 : Retry.Count;
        for (int i = 0; i < count; i++)
            try
            {
                await _interceptorRunner.RunBeforeInterceptors(message, client, handlerFactory, cancellationToken);
                await messageHandler.Handle(message, model, client, cancellationToken);
                await _interceptorRunner.RunAfterInterceptors(message, client, handlerFactory, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                Type type = e.GetType();
                if (Retry.IgnoreExceptions is {Length: > 0})
                    if (Retry.IgnoreExceptions.Any(x => x.IsAssignableFrom(type)))
                        throw;

                if (Retry.DelayBetweenRetries > 0)
                    await Task.Delay(Retry.DelayBetweenRetries, cancellationToken);
            }
    }
}