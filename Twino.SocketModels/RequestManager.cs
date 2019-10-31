using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.SocketModels.Models;
using Twino.SocketModels.Serialization;

namespace Twino.SocketModels
{
    /// <summary>
    /// Provides sending requests and receiving their response messages via web sockets.
    /// With RequestManager, you can send multiple requests async from one single tcp connection
    /// and receive responses of them. Requests and Response are isolated with unique key.
    /// </summary>
    public class RequestManager : IDisposable
    {
        #region Fields - Properties

        /// <summary>
        /// Definition of serialized package as request if it starts with "REQ="
        /// </summary>
        private const string REQUEST_CODE = "REQ";

        /// <summary>
        /// Definition of serialized package as response if it starts with "RES="
        /// </summary>
        private const string RESPONSE_CODE = "RES";

        /// <summary>
        /// Default request timeout in seconds
        /// </summary>
        public int DefaultTimeout { get; set; } = 15;

        // request message :      REQ=[ request_header, request_model ]
        // response message :     RES=[ response_header, response_model ]

        /// <summary>
        /// Subscribed requests to handle
        /// </summary>
        private readonly Dictionary<int, RequestDescriptor> _descriptors = new Dictionary<int, RequestDescriptor>();

        /// <summary>
        /// When client sends the request, a pending request is created for reading it's response or manage it's timeout.
        /// This dictionary keeps all pending requests until they finished. 
        /// </summary>
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new Dictionary<string, PendingRequest>();

        /// <summary>
        /// When a request is created via a socket client, this client is added to handling client list until it's disconnected or manually removed
        /// </summary>
        private readonly List<SocketBase> _handlingClients = new List<SocketBase>();

        /// <summary>
        /// It checks pending requests if they are disconnected or timed out.
        /// and it checked handling clients and removes them from handling clients lists if they are disconnected.
        /// </summary>
        private Timer _cleanupTimer;

        #endregion

        #region Init - Dispose - Subscribe

        /// <summary>
        /// Disposes the object and releases all sources
        /// </summary>
        public void Dispose()
        {
            lock (_handlingClients)
            {
                foreach (SocketBase handlingClient in _handlingClients)
                    handlingClient.MessageReceived -= SenderOnMessageReceived;

                _handlingClients.Clear();
            }

            lock (_pendingRequests)
                _pendingRequests.Clear();

            if (_cleanupTimer != null)
            {
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }

        /// <summary>
        /// Releases the client is handled before for reading response.
        /// After that method is called, responses from this client won't be received.
        /// If there are pending requests, their events will be fired as timeout.
        /// </summary>
        public void ReleaseClient(SocketBase client)
        {
            client.MessageReceived -= SenderOnMessageReceived;

            lock (_handlingClients)
                _handlingClients.Remove(client);
        }

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

            RequestDescriptor descriptor = new RequestDescriptor
                                           {
                                               RequestNo = sampleRequest.Type,
                                               RequestType = typeof(TRequest),
                                               ResponseNo = sampleResponse.Type,
                                               ResponseType = typeof(TResponse),
                                               Action = func
                                           };

            _descriptors.Add(sampleRequest.Type, descriptor);
            return true;
        }

        #endregion

        #region Send Request

        /// <summary>
        /// Created a request to the server and sends the model.
        /// Response will be received via returned task
        /// </summary>
        public async Task<SocketResponse<TResponse>> Request<TResponse>(SocketBase sender, ISocketModel model)
            where TResponse : ISocketModel, new()
        {
            return await Request<TResponse>(sender, model, DefaultTimeout);
        }

        /// <summary>
        /// Created a request to the server and sends the model in specified timeout (in seconds).
        /// Response will be received via returned task
        /// </summary>
        public async Task<SocketResponse<TResponse>> Request<TResponse>(SocketBase sender, ISocketModel model, int timeoutSeconds)
            where TResponse : ISocketModel, new()
        {
            if (_cleanupTimer == null)
                RunCleanupTimer();

            CheckReceiveEvents(sender);

            TaskCompletionSource<SocketResponse<TResponse>> completionSource = new TaskCompletionSource<SocketResponse<TResponse>>();
            RequestHeader header = CreateRequest<TResponse>(model);

            PendingRequest<TResponse> pendingRequest = new PendingRequest<TResponse>();
            pendingRequest.Header = header;
            pendingRequest.Deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            pendingRequest.CompletionSource = completionSource;
            pendingRequest.Sender = sender;

            if (!sender.IsConnected)
            {
                pendingRequest.CompleteAsError();
                return await completionSource.Task;
            }

            lock (_pendingRequests)
                _pendingRequests.Add(header.Unique, pendingRequest);

            byte[] prepared = await PrepareRequest(header, model);
            sender.Send(prepared);

            return await completionSource.Task;
        }

        /// <summary>
        /// Creates request header with sending and receiving models.
        /// </summary>
        private RequestHeader CreateRequest<TResponse>(ISocketModel request) where TResponse : ISocketModel, new()
        {
            TResponse response = new TResponse();
            string unique = Guid.NewGuid() + "-" + Guid.NewGuid();

            return new RequestHeader
                   {
                       Unique = unique,
                       RequestType = request.Type,
                       ResponseType = response.Type
                   };
        }

        #endregion

        #region Handle Request

        /// <summary>
        /// Reads the message from the socket.
        /// If the message is request, finds the subscription and process it.
        /// </summary>
        public async Task HandleRequests(SocketBase sender, string receivedMessage)
        {
            RequestHeader header = ReadHeader<RequestHeader>(REQUEST_CODE, receivedMessage);
            if (header == null)
                return;

            if (!_descriptors.ContainsKey(header.RequestType))
                return;

            RequestDescriptor descriptor = _descriptors[header.RequestType];
            object requestModel = ReadModel(descriptor.RequestType, REQUEST_CODE, receivedMessage);
            if (requestModel == null)
                return;

            await ProcessRequest(sender, descriptor, header, requestModel);
        }

        /// <summary>
        /// Process the request from the client and sends the response. 
        /// </summary>
        private async Task ProcessRequest(SocketBase sender, RequestDescriptor descriptor, RequestHeader header, object requestModel)
        {
            try
            {
                object responseModel = descriptor.Action.DynamicInvoke(requestModel);

                if (responseModel == null)
                {
                    sender.Send(await PrepareResponse(new SocketResponse
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

                byte[] prepared = await PrepareResponse(response, (ISocketModel) responseModel);
                sender.Send(prepared);
            }
            catch
            {
                SocketResponse err = new SocketResponse
                                     {
                                         Status = ResponseStatus.Failed,
                                         Unique = header.Unique,
                                         RequestType = header.RequestType,
                                         ResponseType = header.ResponseType
                                     };
                byte[] prepared = await PrepareResponse(err, null);
                sender.Send(prepared);
            }
        }

        #endregion

        #region Handle Response

        /// <summary>
        /// Adds client to the handling clients list if not added before, and subscribes to message receive event.
        /// </summary>
        public void CheckReceiveEvents(SocketBase sender)
        {
            lock (_handlingClients)
            {
                if (_handlingClients.Contains(sender))
                    return;

                _handlingClients.Add(sender);
            }

            sender.MessageReceived += SenderOnMessageReceived;
        }

        /// <summary>
        /// Called when a message received from the socket.
        /// If message is a response, it's proceed.
        /// </summary>
        private void SenderOnMessageReceived(SocketBase client, string message)
        {
            SocketResponse header = ReadHeader<SocketResponse>(RESPONSE_CODE, message);

            if (header == null || string.IsNullOrEmpty(header.Unique))
                return;

            lock (_pendingRequests)
            {
                if (!_pendingRequests.ContainsKey(header.Unique))
                    return;

                PendingRequest pending = _pendingRequests[header.Unique];

                switch (header.Status)
                {
                    case ResponseStatus.Success:
                        object model = ReadModel(pending.ModelType, RESPONSE_CODE, message);
                        pending.CompleteAsSuccessful(model);
                        break;

                    case ResponseStatus.Failed:
                        pending.CompleteAsFailed();
                        break;

                    case ResponseStatus.Timeout:
                        pending.CompleteAsTimeout();
                        break;

                    case ResponseStatus.ConnectionError:
                        pending.CompleteAsError();
                        break;
                }
            }
        }

        /// <summary>
        /// Runs cleanup timer if it's not running.
        /// This method is called with first request.
        /// </summary>
        private void RunCleanupTimer()
        {
            if (_cleanupTimer != null)
                return;

            List<SocketBase> removingClients = new List<SocketBase>();

            List<KeyValuePair<string, PendingRequest>> errors = new List<KeyValuePair<string, PendingRequest>>();
            List<KeyValuePair<string, PendingRequest>> timeouts = new List<KeyValuePair<string, PendingRequest>>();

            _cleanupTimer = new Timer(state =>
            {
                if (errors.Count > 0)
                    errors.Clear();

                if (timeouts.Count > 0)
                    timeouts.Clear();

                if (removingClients.Count > 0)
                    removingClients.Clear();
                
                lock (_pendingRequests)
                {
                    foreach (KeyValuePair<string, PendingRequest> pair in _pendingRequests)
                    {
                        if (pair.Value.Sender == null || !pair.Value.Sender.IsConnected)
                        {
                            errors.Add(pair);
                            continue;
                        }

                        if (pair.Value.Deadline > DateTime.UtcNow)
                            continue;

                        timeouts.Add(pair);
                    }

                    foreach (var kv in errors) _pendingRequests.Remove(kv.Key);
                    foreach (var kv in timeouts) _pendingRequests.Remove(kv.Key);
                }

                lock (_handlingClients)
                {
                    foreach (SocketBase client in _handlingClients)
                    {
                        if (!client.IsConnected)
                        {
                            client.MessageReceived -= SenderOnMessageReceived;
                            removingClients.Add(client);
                        }
                    }

                    foreach (SocketBase client in removingClients)
                        _handlingClients.Remove(client);
                }

                foreach (KeyValuePair<string, PendingRequest> error in errors)
                    error.Value.CompleteAsError();

                foreach (KeyValuePair<string, PendingRequest> timeout in timeouts)
                    timeout.Value.CompleteAsTimeout();

            }, null, 1000, 1000);
        }

        #endregion

        #region Read - Write

        /// <summary>
        /// Read Request or Response header model from JSON string
        /// </summary>
        private static T ReadHeader<T>(string kind, string message) where T : class, new()
        {
            if (!message.StartsWith(kind + "="))
                return null;

            int headerStart = message.IndexOf('{');
            int headerEnd = message.IndexOf('}');

            if (headerStart < 0 || headerEnd < 0 || headerEnd <= headerStart)
                return null;

            string serialized = message.Substring(headerStart, headerEnd - headerStart + 1);
            T header = System.Text.Json.JsonSerializer.Deserialize<T>(serialized);
            return header;
        }

        /// <summary>
        /// Read Request or Response model from JSON string
        /// </summary>
        private static object ReadModel(Type type, string kind, string message)
        {
            if (!message.StartsWith(kind + "="))
                return null;

            int headerStart = message.IndexOf('{');
            int headerEnd = message.IndexOf('}');

            if (headerStart < 0 || headerEnd < 0 || headerEnd <= headerStart)
                return null;

            int modelStart = message.IndexOf('{', headerEnd);
            int modelEnd = message.LastIndexOf('}');

            string serialized = message.Substring(modelStart, modelEnd - modelStart + 1);
            object model;
            bool critical = typeof(IPerformanceCriticalModel).IsAssignableFrom(type);
            if (critical)
            {
                IPerformanceCriticalModel criticalModel = (IPerformanceCriticalModel) Activator.CreateInstance(type);

                LightJsonReader reader = new LightJsonReader(serialized);
                reader.StartObject();
                criticalModel.Deserialize(reader);
                reader.EndObject();

                model = criticalModel;
            }
            else
                model = System.Text.Json.JsonSerializer.Deserialize(serialized, type);

            return model;
        }

        /// <summary>
        /// Creates request websocket message from header and model instances
        /// </summary>
        private static async Task<byte[]> PrepareRequest(RequestHeader header, ISocketModel model)
        {
            LightJsonWriter writer = new LightJsonWriter();

            await writer.Writer.WriteRawAsync(REQUEST_CODE + "=");
            await writer.Writer.WriteStartArrayAsync();

            //header
            writer.StartObject();
            writer.Write("unique", header.Unique);
            writer.Write("requestType", header.RequestType);
            writer.Write("responseType", header.ResponseType);
            writer.EndObject();
            await writer.Writer.WriteRawAsync(",");

            if (model is IPerformanceCriticalModel critical)
            {
                writer.StartObject();
                critical.Serialize(writer);
                writer.EndObject();
            }
            else
                await writer.Writer.WriteRawAsync(System.Text.Json.JsonSerializer.Serialize(model));

            await writer.Writer.WriteEndArrayAsync();

            string message = writer.GetResult();
            return await WebSocketWriter.CreateFromUTF8Async(message);
        }

        /// <summary>
        /// Creates response websocket message from header and model instances
        /// </summary>
        private static async Task<byte[]> PrepareResponse(SocketResponse header, ISocketModel model)
        {
            LightJsonWriter writer = new LightJsonWriter();
            await writer.Writer.WriteRawAsync(RESPONSE_CODE + "=");
            await writer.Writer.WriteStartArrayAsync();

            //header
            writer.StartObject();
            writer.Write("unique", header.Unique);
            writer.Write("requestType", header.RequestType);
            writer.Write("responseType", header.ResponseType);
            writer.Write("status", header.Status);
            writer.EndObject();
            await writer.Writer.WriteRawAsync(",");

            if (model is IPerformanceCriticalModel critical)
            {
                writer.StartObject();
                critical.Serialize(writer);
                writer.EndObject();
            }
            else
                await writer.Writer.WriteRawAsync(System.Text.Json.JsonSerializer.Serialize(model));

            await writer.Writer.WriteEndArrayAsync();

            string message = writer.GetResult();
            return await WebSocketWriter.CreateFromUTF8Async(message);
        }

        #endregion
    }
}