using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
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
            _registration = registration as DirectHandlerRegistration;
            ResolveAttributes(_registration!.ConsumerType);
            ResolveDirectAttributes();
        }

        private void ResolveDirectAttributes()
        {
            AutoResponseAttribute responseAttribute = _consumerType.GetCustomAttribute<AutoResponseAttribute>();
            if (responseAttribute != null)
            {
                if (responseAttribute.Response == AutoResponse.All || responseAttribute.Response == AutoResponse.OnSuccess)
                    SendPositiveResponse = true;

                if (responseAttribute.Response == AutoResponse.All || responseAttribute.Response == AutoResponse.OnError)
                {
                    SendNegativeResponse = true;
                    NegativeReason = responseAttribute.Error;
                }
            }
        }

        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            TModel t = (TModel) model;
            ProvidedHandler providedHandler = null;

            try
            {
                if (_messageHandler != null)
                {
                    await RunBeforeInterceptors(message, client);
                    await Consume(_messageHandler, message, t, client);
                    await RunAfterInterceptors(message, client);
                }
                else if (_consumerFactoryCreator != null)
                {
                    IHandlerFactory handlerFactory = _consumerFactoryCreator();
                    providedHandler = handlerFactory.CreateHandler(_consumerType);
                    IDirectMessageHandler<TModel> messageHandler = (IDirectMessageHandler<TModel>) providedHandler.Service;
                    await RunBeforeInterceptors(message, client, handlerFactory);
                    await Consume(messageHandler, message, t, client);
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

        private async Task Consume(IDirectMessageHandler<TModel> messageHandler, HorseMessage message, TModel model, HorseClient client)
        {
            if (Retry == null)
            {
                await messageHandler.Handle(message, model, client);
                return;
            }

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    await messageHandler.Handle(message, model, client);
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
                }
            }
        }
        
        /// <summary>
        /// Run before interceptors
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <param name="handlerFactory"></param>
        protected async Task RunBeforeInterceptors(HorseMessage message, HorseClient client, IHandlerFactory handlerFactory = null)
        {
            if(_registration.IntercetorDescriptors.Count == 0) return;
            var beforeInterceptors = _registration.IntercetorDescriptors.Where(m => m.RunBefore);
            IEnumerable<IHorseInterceptor> interceptors = handlerFactory is null
                ? beforeInterceptors.Select(m => m.Instance)
                : beforeInterceptors.Select(m => handlerFactory.CreateInterceptor(m.InterceptorType));

            foreach (var interceptor in interceptors)
                await interceptor!.Intercept(message, client);
        }

        /// <summary>
        /// Run after interceptors
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <param name="handlerFactory"></param>
        protected async Task RunAfterInterceptors(HorseMessage message, HorseClient client, IHandlerFactory handlerFactory = null)
        {
            if(_registration.IntercetorDescriptors.Count == 0) return;
            var afterInterceptors = _registration.IntercetorDescriptors.Where(m => !m.RunBefore);
            IEnumerable<IHorseInterceptor> interceptors = handlerFactory is null
                ? afterInterceptors.Select(m => m.Instance)
                : afterInterceptors.Select(m => handlerFactory.CreateInterceptor(m.InterceptorType));

            foreach (var interceptor in interceptors)
                try
                {
                    await interceptor!.Intercept(message, client);
                }
                catch (Exception e)
                {
                    client.OnException(e, message);
                }
        }
    }
}