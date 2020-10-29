using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class RequestHandlerExecuter<TRequest, TResponse> : ConsumerExecuter
    {
        private readonly Type _handlerType;
        private readonly ITwinoRequestHandler<TRequest, TResponse> _handler;
        private readonly Func<IConsumerFactory> _handlerFactoryCreator;

        public RequestHandlerExecuter(Type handlerType, ITwinoRequestHandler<TRequest, TResponse> handler, Func<IConsumerFactory> handlerFactoryCreator)
        {
            _handlerType = handlerType;
            _handler = handler;
            _handlerFactoryCreator = handlerFactoryCreator;
            ResolveAttributes(_handlerType, typeof(TRequest));
        }

        public override async Task Execute(TmqClient client, TwinoMessage message, object model)
        {
            Exception exception = null;
            IConsumerFactory consumerFactory = null;
            bool respond = false;

            try
            {
                TRequest requestModel = (TRequest) model;
                ITwinoRequestHandler<TRequest, TResponse> handler;

                if (_handler != null)
                    handler = _handler;
                else if (_handlerFactoryCreator != null)
                {
                    consumerFactory = _handlerFactoryCreator();
                    object consumerObject = await consumerFactory.CreateConsumer(_handlerType);
                    handler = (ITwinoRequestHandler<TRequest, TResponse>) consumerObject;
                }
                else
                    throw new ArgumentNullException("There is no consumer defined");

                try
                {
                    TResponse responseModel = await Handle(handler, requestModel, message, client);
                    TwinoResultCode code = responseModel is null ? TwinoResultCode.NoContent : TwinoResultCode.Ok;
                    TwinoMessage responseMessage = message.CreateResponse(code);

                    if (responseModel != null)
                        responseMessage.Serialize(responseModel, client.JsonSerializer);

                    respond = true;
                    await client.SendAsync(responseMessage);
                }
                catch (Exception e)
                {
                    ErrorResponse errorModel = await handler.OnError(e, requestModel, message, client);

                    if (errorModel.ResultCode == TwinoResultCode.Ok)
                        errorModel.ResultCode = TwinoResultCode.Failed;

                    TwinoMessage responseMessage = message.CreateResponse(errorModel.ResultCode);

                    if (!string.IsNullOrEmpty(errorModel.Reason))
                        responseMessage.SetStringContent(errorModel.Reason);

                    respond = true;
                    await client.SendAsync(responseMessage);
                    throw;
                }
            }
            catch (Exception e)
            {
                if (!respond)
                {
                    try
                    {
                        TwinoMessage response = message.CreateResponse(TwinoResultCode.InternalServerError);
                        await client.SendAsync(response);
                    }
                    catch
                    {
                    }
                }

                await SendExceptions(client, e);
                exception = e;
            }
            finally
            {
                if (consumerFactory != null)
                    consumerFactory.Consumed(exception);
            }
        }

        private async Task<TResponse> Handle(ITwinoRequestHandler<TRequest, TResponse> handler, TRequest request, TwinoMessage message, TmqClient client)
        {
            if (Retry == null)
                return await handler.Handle(request, message, client);

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    return await handler.Handle(request, message, client);
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

            throw new OperationCanceledException("Reached to maximum retry count and execution could not be completed");
        }
    }
}