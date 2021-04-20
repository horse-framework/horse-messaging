using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Internal;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client
{
    //todo: client data properties
    //todo: subscribe on connect

    /// <inheritdoc />
    public class HorseClient<TIdentifier> : HorseClient
    {
    }

    /// <summary>
    /// HMQ Client class
    /// Can be used directly with event subscriptions
    /// Or can be base class to a derived Client class and provides virtual methods for all events
    /// </summary>
    public class HorseClient : IDisposable
    {
        #region Properties

        /// <summary>
        /// Custom tag object for client.
        /// If you need to attach an other to HorseClient, use this property.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// The waiting time before reconnecting, after disconnection.
        /// Default value ise 3 seconds.
        /// </summary>
        public TimeSpan ReconnectWait
        {
            get => _reconnectWait;
            set
            {
                _reconnectWait = value;
                UpdateReconnectTimer();
            }
        }

        /// <summary>
        /// Connection lifetime.
        /// It's reset after reconnection.
        /// </summary>
        public TimeSpan Lifetime { get; set; }

        /// <summary>
        /// Defined remote host.
        /// If Connect methods are used with remote host parameter, this value is updated.
        /// </summary>
        public string RemoteHost { get; set; }

        /// <summary>
        /// Internal connected event
        /// </summary>
        internal Action<HorseClient> ConnectedAction { get; set; }

        /// <summary>
        /// Internal disconnected event
        /// </summary>
        internal Action<HorseClient> DisconnectedAction { get; set; }

        /// <summary>
        /// Internal error event
        /// </summary>
        internal Action<Exception> ErrorAction { get; set; }

        /// <summary>
        /// If true, automatically subscribes all implemented queues and channels.
        /// </summary>
        public bool AutoSubscribe { get; set; } = true;

        /// <summary>
        /// If true, disconnected from server when auto join fails
        /// </summary>
        public bool DisconnectionOnAutoJoinFailure { get; set; } = true;

        /// <summary>
        /// Returns true if connected
        /// </summary>
        public bool IsConnected => _socket != null && _socket.IsConnected;

        /// <summary>
        /// Unique Id generator for sending messages
        /// </summary>
        public IUniqueIdGenerator UniqueIdGenerator { get; set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// If true, acknowledge message will be sent automatically if message requires.
        /// </summary>
        public bool AutoAcknowledge { get; set; }

        /// <summary>
        /// If true, response messages will trigger on message received events.
        /// If false, response messages are proceed silently.
        /// </summary>
        public bool CatchResponseMessages { get; set; }

        /// <summary>
        /// Maximum time to wait response message
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum time for waiting next message of a pull request 
        /// </summary>
        public TimeSpan PullTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Unique client id
        /// </summary>
        private string _clientId;

        /// <summary>
        /// Client Id.
        /// If a value is set before connection, the value will be kept.
        /// If value is not set, a unique value will be generated with IUniqueIdGenerator before connect.
        /// </summary>
        public string ClientId
        {
            get => _clientId;
            set
            {
                if (!string.IsNullOrEmpty(_clientId))
                    throw new InvalidOperationException("Client Id cannot be change after connection established");

                _clientId = value;
            }
        }

        /// <summary>
        /// Response message tracker of the client
        /// </summary>
        internal MessageTracker Tracker { get; private set; }

        /// <summary>
        /// HMQ Client Direct message management object
        /// </summary>
        public DirectOperator Direct { get; }

        /// <summary>
        /// HMQ Client Direct message management object
        /// </summary>
        public ChannelOperator Channel { get; }

        /// <summary>
        /// HMQ Client Queue Management object
        /// </summary>
        public QueueOperator Queue { get; }

        /// <summary>
        /// HMQ Client Connection Management object
        /// </summary>
        public ConnectionOperator Connection { get; }

        /// <summary>
        /// HMQ Client Router Management object
        /// </summary>
        public RouterOperator Router { get; }

        /// <summary>
        /// Event manage of the client
        /// </summary>
        internal EventManager Event { get; set; }

        /// <summary>
        /// Serializer object for horse messages
        /// </summary>
        public IMessageContentSerializer MessageSerializer { get; set; } = new NewtonsoftContentSerializer();

        internal IServiceProvider Provider { get; set; }

        #endregion

        #region Constructors - Destructors

        private TimeSpan _reconnectWait = TimeSpan.FromSeconds(3);
        private HorseSocket _socket;
        private readonly ConnectionData _data = new ConnectionData();
        private bool _autoConnect;
        private Timer _reconnectTimer;

        /// <summary>
        /// Creates new HMQ protocol client
        /// </summary>
        public HorseClient()
        {
            Channel = new ChannelOperator(this);
            Direct = new DirectOperator(this);
            Queue = new QueueOperator(this);
            Connection = new ConnectionOperator(this);
            Router = new RouterOperator(this);
            Event = new EventManager();

            Tracker = new MessageTracker(this);
            Tracker.Run();
        }

        /// <summary>
        /// Releases all resources of the client
        /// </summary>
        public void Dispose()
        {
            Tracker?.Dispose();
            Queue.Dispose();
        }


        private void UpdateReconnectTimer()
        {
            if (_reconnectWait == TimeSpan.Zero)
                return;

            if (_reconnectTimer != null)
            {
                int ms = Convert.ToInt32(_reconnectWait.TotalMilliseconds);
                _reconnectTimer = new Timer(s =>
                {
                    if (!_autoConnect || IsConnected)
                        return;

                    if (_socket != null)
                    {
                        if (_socket.IsConnecting)
                            return;

                        _socket.Disconnect();
                    }

                    _socket = new HorseSocket(this, _data);
                    _ = _socket.ConnectAsync(RemoteHost);
                }, null, ms, ms);
            }
        }

        private void SetAutoReconnect(bool value)
        {
            _autoConnect = value;
            if (value)
            {
                if (_reconnectTimer != null)
                    return;

                UpdateReconnectTimer();
            }
            else
            {
                if (_reconnectTimer != null)
                {
                    _reconnectTimer.Dispose();
                    _reconnectTimer = null;
                }
            }
        }

        #endregion

        #region Connection Data

        /// <summary>
        /// Sets client type information of the client
        /// </summary>
        public void SetClientType(string type)
        {
            if (_data.Properties.ContainsKey(HorseHeaders.CLIENT_TYPE))
                _data.Properties[HorseHeaders.CLIENT_TYPE] = type;
            else
                _data.Properties.Add(HorseHeaders.CLIENT_TYPE, type);
        }

        /// <summary>
        /// Sets client name information of the client
        /// </summary>
        public void SetClientName(string name)
        {
            if (_data.Properties.ContainsKey(HorseHeaders.CLIENT_NAME))
                _data.Properties[HorseHeaders.CLIENT_NAME] = name;
            else
                _data.Properties.Add(HorseHeaders.CLIENT_NAME, name);
        }

        internal void SetClientId(string id)
        {
            _clientId = id;
        }

        /// <summary>
        /// Sets client token information of the client
        /// </summary>
        public void SetClientToken(string token)
        {
            if (_data.Properties.ContainsKey(HorseHeaders.CLIENT_TOKEN))
                _data.Properties[HorseHeaders.CLIENT_TOKEN] = token;
            else
                _data.Properties.Add(HorseHeaders.CLIENT_TOKEN, token);
        }

        #endregion

        #region Connect - Read

        /// <summary>
        /// Connects to RemoteHost
        /// </summary>
        public void Connect()
        {
            Connect(RemoteHost);
        }

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public void Connect(string host)
        {
            DnsResolver resolver = new DnsResolver();
            Connect(resolver.Resolve(host));
        }

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public void Connect(DnsInfo host)
        {
            if (string.IsNullOrEmpty(_clientId))
                _clientId = UniqueIdGenerator.Create();

            /* todo
            if (host.Protocol != Core.Protocol.Hmq)
                throw new NotSupportedException("Only Horse protocol is supported");
                */

            SetAutoReconnect(true);
            _socket = new HorseSocket(this, _data);
            _socket.Connect(host);
        }

        /// <summary>
        /// Connects to RemoteHost
        /// </summary>
        public Task ConnectAsync()
        {
            return ConnectAsync(RemoteHost);
        }

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public Task ConnectAsync(string host)
        {
            DnsResolver resolver = new DnsResolver();
            return ConnectAsync(resolver.Resolve(host));
        }

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public Task ConnectAsync(DnsInfo host)
        {
            if (string.IsNullOrEmpty(_clientId))
                _clientId = UniqueIdGenerator.Create();

            /* todo
            if (host.Protocol != Core.Protocol.Hmq)
                throw new NotSupportedException("Only Horse protocol is supported");
                */
            
            SetAutoReconnect(true);
            _socket = new HorseSocket(this, _data);
            return _socket.ConnectAsync(host);
        }

        /// <summary>
        /// Disconnected from the server
        /// </summary>
        public void Disconnect()
        {
            SetAutoReconnect(false);

            if (_socket != null)
            {
                _socket.Disconnect();
                _socket = null;
            }
        }

        #endregion

        #region Send

        /// <summary>
        /// Sends a HMQ message
        /// </summary>
        public bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);

            if (_socket == null)
                return false;

            return _socket.Send(data);
        }

        /// <summary>
        /// Sends a HMQ message
        /// </summary>
        public async Task<HorseResult> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);

            if (_socket == null)
                return new HorseResult(HorseResultCode.SendError);

            bool sent = await _socket.SendAsync(data);
            return sent ? HorseResult.Ok() : new HorseResult(HorseResultCode.SendError);
        }

        /// <summary>
        /// Sends raw message
        /// </summary>
        public Task<bool> SendAsync(byte[] rawData)
        {
            return _socket.SendAsync(rawData);
        }

        /// <summary>
        /// Sends a HMQ message and waits for acknowledge
        /// </summary>
        public async Task<HorseResult> SendAndGetAck(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);
            message.WaitResponse = true;

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            if (message.Type == MessageType.DirectMessage)
                message.HighPriority = true;

            if (additionalHeaders != null)
                foreach (KeyValuePair<string, string> pair in additionalHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (string.IsNullOrEmpty(message.MessageId))
                throw new ArgumentNullException("Messages without unique id cannot be acknowledged");

            return await WaitResponse(message, true);
        }

        /// <summary>
        /// Sends a message, waits response and deserializes JSON response to T template type
        /// </summary>
        public async Task<HorseModelResult<T>> SendAndGetJson<T>(HorseMessage message)
        {
            message.WaitResponse = true;

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            Task<HorseMessage> task = Tracker.Track(message);
            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
                return new HorseModelResult<T>(new HorseResult(HorseResultCode.SendError));

            HorseMessage response = await task;
            if (response?.Content == null || response.Length == 0 || response.Content.Length == 0)
                return HorseModelResult<T>.FromContentType(message.ContentType);

            T model = response.Deserialize<T>(MessageSerializer);
            return new HorseModelResult<T>(HorseResult.Ok(), model);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<HorseResult> SendResponseAsync<TModel>(HorseMessage requestMessage, TModel responseModel)
        {
            HorseMessage response = requestMessage.CreateResponse(HorseResultCode.Ok);
            response.Serialize(responseModel, MessageSerializer);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, string responseContent)
        {
            HorseMessage response = requestMessage.CreateResponse(HorseResultCode.Ok);
            response.SetStringContent(responseContent);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<HorseResult> SendResponseAsync(HorseMessage requestMessage, Stream content)
        {
            HorseMessage response = requestMessage.CreateResponse(HorseResultCode.Ok);
            response.Content = new MemoryStream();
            await content.CopyToAsync(response.Content);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends the message and waits for response
        /// </summary>
        public async Task<HorseMessage> Request(HorseMessage message)
        {
            message.WaitResponse = true;
            message.HighPriority = false;
            message.SetMessageId(UniqueIdGenerator.Create());

            Task<HorseMessage> task = Tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                Tracker.Forget(message);
                return message.CreateResponse(sent.Code);
            }

            HorseMessage response = await task;
            if (response == null)
                response = message.CreateResponse(HorseResultCode.RequestTimeout);

            return response;
        }

        #endregion

        #region Acknowledge - Response

        /// <summary>
        /// Sends unacknowledge message for the message.
        /// </summary>
        public async Task<HorseResult> SendNegativeAck(HorseMessage message, string reason = null)
        {
            if (string.IsNullOrEmpty(reason))
                reason = HorseHeaders.NACK_REASON_NONE;

            HorseMessage ack = message.CreateAcknowledge(reason);
            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends unacknowledge message for the message.
        /// </summary>
        public async Task<HorseResult> SendAck(HorseMessage message)
        {
            HorseMessage ack = message.CreateAcknowledge();
            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends message.
        /// if verify requires, waits response and checkes status code of the response.
        /// returns true if Ok.
        /// </summary>
        protected internal async Task<HorseResult> WaitResponse(HorseMessage message, bool waitForResponse)
        {
            Task<HorseMessage> task = null;
            if (waitForResponse)
                task = Tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                if (waitForResponse)
                    Tracker.Forget(message);

                return new HorseResult(HorseResultCode.SendError);
            }

            if (waitForResponse)
            {
                HorseMessage response = await task;
                if (response == null)
                    return HorseResult.Timeout();

                HorseResult result = new HorseResult((HorseResultCode) response.ContentType);
                result.Message = response;

                if (response.HasHeader && string.IsNullOrEmpty(result.Reason))
                    result.Reason = response.FindHeader(HorseHeaders.NEGATIVE_ACKNOWLEDGE_REASON);

                return result;
            }

            return HorseResult.Ok();
        }

        #endregion

        #region Events

        internal async Task<bool> EventSubscription(string eventName, bool subscribe, string queueName)
        {
            ushort ct = subscribe ? (ushort) 1 : (ushort) 0;
            HorseMessage message = new HorseMessage(MessageType.Event, eventName, ct);
            message.SetMessageId(UniqueIdGenerator.Create());
            message.WaitResponse = true;

            if (!string.IsNullOrEmpty(queueName))
                message.AddHeader(HorseHeaders.QUEUE_NAME, queueName);

            Task<HorseMessage> task = Tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                Tracker.Forget(message);
                return false;
            }

            HorseMessage response = await task;
            if (response == null)
                return false;

            HorseResultCode code = (HorseResultCode) response.ContentType;
            return code == HorseResultCode.Ok;
        }

        #endregion

        internal async Task OnMessageReceived(HorseMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Server:
                    if (message.ContentType == KnownContentTypes.Accepted)
                        SetClientId(message.Target);
                    break;

                case MessageType.Terminate:
                    _socket.Disconnect();
                    _socket = null;
                    break;

                case MessageType.Pong:
                    _socket.KeepAlive();
                    break;

                case MessageType.Ping:
                    _socket.Pong();
                    break;

                case MessageType.Response:
                    Tracker.Process(message);

                    if (CatchResponseMessages)
                    {
                        //SetOnMessageReceived(message);
                    }

                    break;

                case MessageType.Event:
                    _ = Event.TriggerEvents(this, message);
                    break;

                case MessageType.QueueMessage:

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    await Queue.OnQueueMessage(message);
                    //SetOnMessageReceived(message);
                    break;

                case MessageType.DirectMessage:

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    await Direct.OnDirectMessage(message);
                    //SetOnMessageReceived(message);
                    break;
            }
        }

        internal void OnException(string hint, Exception e, HorseMessage message)
        {
            throw new NotImplementedException();
        }


        /*
         *

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            HorseClient horseClient = (HorseClient) client;
            
            horseClient.Events = Events;
            if (horseClient.DeliveryContainer is TypeDeliveryContainer container)
                container.DefaultConfiguration = Observer.Configurator;

            if (ContentSerializer != null)
                horseClient.MessageSerializer = ContentSerializer;

            base.ClientConnected(client);

            if (AutoSubscribe)
                _ = SubscribeToAllImplementedQueues(true, DisconnectionOnAutoJoinFailure);
        }

        /// <summary>
        /// Subscribes to all implemenetd queues (Implemented with IQueueConsumer interface)
        /// </summary>
        /// <param name="verify">If true, waits response from server for each join operation</param>
        /// <param name="disconnectOnFail">If any of queues fails to subscribe, disconnected from server</param>
        /// <param name="silent">If true, errors are hidden, no exception thrown</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">Thrown if there is no consumer initialized</exception>
        /// <exception cref="HorseSocketException">Thrown if not connected to server</exception>
        public async Task<bool> SubscribeToAllImplementedQueues(bool verify, bool disconnectOnFail = true, bool silent = true)
        {
            if (_observer == null)
            {
                if (silent)
                    return false;

                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");
            }

            HorseClient client = GetClient();
            if (client == null)
            {
                if (silent)
                    return false;

                throw new HorseSocketException("There is no active connection");
            }

            Tuple<string, TypeDeliveryDescriptor>[] items = _observer.GetSubscribedQueues();
            foreach (Tuple<string, TypeDeliveryDescriptor> item in items)
            {
                HorseMessage msg = item.Item2.CreateMessage(MessageType.Server, item.Item1, KnownContentTypes.Subscribe);
                HorseResult joinResult = await client.SendAndGetAck(msg);
                if (joinResult.Code == HorseResultCode.Ok)
                    continue;

                if (disconnectOnFail)
                    client.Disconnect();

                if (!silent)
                    throw new HorseQueueException($"Can't subscribe to {item.Item1} queue: {joinResult.Reason} ({joinResult.Code})");

                return false;
            }

            return true;
        }

         * 
         */
    }
}