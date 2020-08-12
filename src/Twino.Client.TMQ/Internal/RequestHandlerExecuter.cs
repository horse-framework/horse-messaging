using System;
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

        public override async Task Execute(TmqClient client, TmqMessage message, object model)
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
                    TResponse responseModel = await handler.Handle(requestModel, message, client);
                    TwinoResultCode code = responseModel is null ? TwinoResultCode.NoContent : TwinoResultCode.Ok;
                    TmqMessage responseMessage = message.CreateResponse(code);

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

                    TmqMessage responseMessage = message.CreateResponse(errorModel.ResultCode);

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
                        TmqMessage response = message.CreateResponse(TwinoResultCode.InternalServerError);
                        await client.SendAsync(response);
                    }
                    catch
                    {
                    }
                }

                await SendExceptions(client, e);
                exception = e;
                throw;
            }
            finally
            {
                if (consumerFactory != null)
                    consumerFactory.Consumed(exception);
            }
        }
    }
}