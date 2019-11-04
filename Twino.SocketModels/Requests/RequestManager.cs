using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core;

namespace Twino.SocketModels.Requests
{
    /// <summary>
    /// Provides sending requests and receiving their response messages via web sockets.
    /// With RequestManager, you can send multiple requests async from one single tcp connection
    /// and receive responses of them. Requests and Response are isolated with unique key.
    /// </summary>
    public class RequestManager
    {
        #region Fields - Properties

        /// <summary>
        /// Default request timeout in seconds
        /// </summary>
        public static int DefaultTimeout { get; set; } = 15;

        // request message :      REQ=[ request_header, request_model ]
        // response message :     RES=[ response_header, response_model ]

        /// <summary>
        /// Subscribed requests to handle
        /// </summary>
        private readonly Dictionary<int, RequestDescriptor> _descriptors = new Dictionary<int, RequestDescriptor>();

        #endregion

        #region Init - Subscribe

        /// <summary>
        /// Creates a subscription for reading the request from clients.
        /// </summary>
        public bool On<TRequest, TResponse>(Func<TRequest, TResponse> func)
            where TRequest : ISocketModel, new()
            where TResponse : ISocketModel, new()
        {
            TRequest sampleRequest = new TRequest();
            TResponse sampleResponse = new TResponse();

            if (_descriptors.ContainsKey(sampleRequest.Type))
                return false;

            RequestDescriptor descriptor = new RequestDescriptor<TRequest, TResponse>
                                           {
                                               RequestNo = sampleRequest.Type,
                                               RequestType = typeof(TRequest),
                                               ResponseNo = sampleResponse.Type,
                                               ResponseType = typeof(TResponse),
                                               Action = func,
                                               IsAsync = false
                                           };

            _descriptors.Add(sampleRequest.Type, descriptor);
            return true;
        }

        /// <summary>
        /// Creates a subscription for reading the request from clients.
        /// </summary>
        public bool OnScheduled<TRequest, TResponse>(Func<TRequest, Task<TResponse>> func)
            where TRequest : ISocketModel, new()
            where TResponse : ISocketModel, new()
        {
            TRequest sampleRequest = new TRequest();
            TResponse sampleResponse = new TResponse();

            if (_descriptors.ContainsKey(sampleRequest.Type))
                return false;

            RequestDescriptor descriptor = new RequestDescriptor<TRequest, TResponse>
                                           {
                                               RequestNo = sampleRequest.Type,
                                               RequestType = typeof(TRequest),
                                               ResponseNo = sampleResponse.Type,
                                               ResponseType = typeof(TResponse),
                                               ActionAsync = func,
                                               IsAsync = true
                                           };

            _descriptors.Add(sampleRequest.Type, descriptor);
            return true;
        }

        #endregion

        #region Handle Request

        /// <summary>
        /// Reads the message from the socket.
        /// If the message is request, finds the subscription and process it.
        /// </summary>
        public async Task HandleRequests(SocketBase sender, string receivedMessage)
        {
            RequestHeader header = TwinoRequestSerializer.DeserializeHeader<RequestHeader>(TwinoRequestSerializer.REQUEST_CODE, receivedMessage);
            if (header == null)
                return;

            if (!_descriptors.ContainsKey(header.RequestType))
                return;

            RequestDescriptor descriptor = _descriptors[header.RequestType];
            object requestModel = TwinoRequestSerializer.DeserializeModel(descriptor.RequestType, TwinoRequestSerializer.REQUEST_CODE, receivedMessage);
            if (requestModel == null)
                return;

            await ProcessRequest(sender, descriptor, header, requestModel);
        }

        /// <summary>
        /// Process the request from the client and sends the response. 
        /// </summary>
        private static async Task ProcessRequest(SocketBase sender, RequestDescriptor descriptor, RequestHeader header, object requestModel)
        {
            try
            {
                object responseModel = await descriptor.Do(requestModel);

                if (responseModel == null)
                {
                    sender.Send(await TwinoRequestSerializer.SerializeResponse(new SocketResponse
                                                                               {
                                                                                   Status = ResponseStatus.Failed,
                                                                                   Unique = header.Unique,
                                                                                   RequestType = header.RequestType,
                                                                                   ResponseType = header.ResponseType
                                                                               }, null));
                    return;
                }

                SocketResponse response = new SocketResponse
                                          {
                                              Status = ResponseStatus.Success,
                                              Unique = header.Unique,
                                              RequestType = header.RequestType,
                                              ResponseType = header.ResponseType
                                          };

                byte[] prepared = await TwinoRequestSerializer.SerializeResponse(response, (ISocketModel) responseModel);
                sender.Send(prepared);
            }
            catch (Exception e)
            {
                SocketResponse err = new SocketResponse
                                     {
                                         Status = ResponseStatus.Failed,
                                         Unique = header.Unique,
                                         RequestType = header.RequestType,
                                         ResponseType = header.ResponseType
                                     };
                byte[] prepared = await TwinoRequestSerializer.SerializeResponse(err, null);
                sender.Send(prepared);
            }
        }

        #endregion
    }
}