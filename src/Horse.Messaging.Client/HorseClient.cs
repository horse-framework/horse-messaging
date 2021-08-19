using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Client.Queues.Internal;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Delegate for HorseClient OnMessageReceived event
    /// </summary>
    public delegate void ClientMessageReceivedHandler(HorseClient client, HorseMessage message);

    /// <summary>
    /// Delegate for HorseClient Connected and Disconnected events
    /// </summary>
    public delegate void ClientConnectionHandler(HorseClient client);

    /// <summary>
    /// Delete for HorseClient Error event
    /// </summary>
    public delegate void ClientErrorHandler(HorseClient client, Exception exception, HorseMessage horseMessage = null);

    /// <inheritdoc />
    public class HorseClient<TIdentifier> : HorseClient
    {
        /// <summary>
        /// Creates new Horse Client
        /// </summary>
        public HorseClient()
        {
            Cache = new HorseCache<TIdentifier>(this);
        }
    }

    /// <summary>
    /// Horse Client class
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
                if (_reconnectTimer is not null) throw new InvalidOperationException("Reconnect wait time cannot modify when client is connected.");
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
        /// Internal message received event
        /// </summary>
        internal Action<HorseMessage> MessageReceivedAction { get; set; }

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
        /// Triggered when client receives a message
        /// </summary>
        public event ClientMessageReceivedHandler MessageReceived;

        /// <summary>
        /// Triggered when client connects to the server
        /// </summary>
        public event ClientConnectionHandler Connected;

        /// <summary>
        /// Triggered when client disconnects from the server
        /// </summary>
        public event ClientConnectionHandler Disconnected;

        /// <summary>
        /// Triggered when an exception is thrown in client operations
        /// </summary>
        public event ClientErrorHandler Error;

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
        internal MessageTracker Tracker { get; }

        /// <summary>
        /// Cache manager for Horse Client
        /// </summary>
        public IHorseCache Cache { get; protected init; }

        /// <summary>
        /// Horse Client Direct message management object
        /// </summary>
        public DirectOperator Direct { get; protected init; }

        /// <summary>
        /// Horse Client Direct message management object
        /// </summary>
        public ChannelOperator Channel { get; protected init; }

        /// <summary>
        /// Horse Client Queue Management object
        /// </summary>
        public QueueOperator Queue { get; protected init; }

        /// <summary>
        /// Horse Client Connection Management object
        /// </summary>
        public ConnectionOperator Connection { get; protected init; }

        /// <summary>
        /// Horse Client Router Management object
        /// </summary>
        public RouterOperator Router { get; protected init; }

        /// <summary>
        /// Horse Event Management object
        /// </summary>
        public EventOperator Event { get; protected init; }

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
        /// Creates new horse client
        /// </summary>
        public HorseClient()
        {
            Cache = new HorseCache(this);
            Channel = new ChannelOperator(this);
            Direct = new DirectOperator(this);
            Queue = new QueueOperator(this);
            Connection = new ConnectionOperator(this);
            Router = new RouterOperator(this);
            Event = new EventOperator(this);
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

            if (_reconnectTimer == null)
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

                    Connect(RemoteHost);
                }, null, ms, ms);
            }
        }

        private void SetAutoReconnect(bool value)
        {
            _autoConnect = value;
            if (value)
            {
                if (_reconnectTimer != null) return;

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

        /// <summary>
        /// Adds new property to client.
        /// The property key and value is sent to server when connected
        /// </summary>
        public void AddProperty(string key, string value)
        {
            _data.Properties.Add(key, value);
        }

        /// <summary>
        /// Removes a data from the client
        /// </summary>
        public void RemoveProperty(string key)
        {
            _data.Properties.Remove(key);
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
            try
            {
                _socket = new HorseSocket(this, _data);
                _socket.Connect(host);
            }
            catch (Exception e)
            {
                OnException(e);
            }
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
        /// Sends a Horse message
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
        /// Sends a Horse message
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
        /// Sends a Horse message and waits for acknowledge
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

            if (string.IsNullOrEmpty(message.MessageId))
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
            {
                message.WaitResponse = true;
                if (string.IsNullOrEmpty(message.MessageId))
                    message.SetMessageId(UniqueIdGenerator.Create());

                task = Tracker.Track(message);
            }

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
                case MessageType.Channel:
                    await Channel.OnChannelMessage(message);
                    break;

                case MessageType.QueueMessage:

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    InvokeMessageReceived(message);
                    await Queue.OnQueueMessage(message);
                    break;

                case MessageType.DirectMessage:

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    InvokeMessageReceived(message);
                    await Direct.OnDirectMessage(message);
                    break;

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
                        InvokeMessageReceived(message);

                    break;

                case MessageType.Event:
                    _ = Event.OnEventMessage(message);
                    break;
            }
        }

        internal void InvokeMessageReceived(HorseMessage message)
        {
            try
            {
                MessageReceived?.Invoke(this, message);
                MessageReceivedAction?.Invoke(message);
            }
            catch (Exception exception)
            {
                OnException(exception, message);
            }
        }

        internal void OnException(Exception e, HorseMessage message = null)
        {
            Error?.Invoke(this, e, message);
            ErrorAction?.Invoke(e);
        }

        internal async Task OnConnected()
        {
            Connected?.Invoke(this);
            ConnectedAction?.Invoke(this);

            if (!AutoSubscribe)
                return;

            foreach (QueueConsumerRegistration registration in Queue.Registrations)
            {
                HorseResult joinResult = await Queue.Subscribe(registration.QueueName, true);
                if (joinResult.Code == HorseResultCode.Ok)
                    continue;

                if (DisconnectionOnAutoJoinFailure)
                {
                    if (_socket != null)
                    {
                        _socket.Disconnect();
                        _socket = null;

                        HorseQueueException exception = new($"Can't subscribe to {registration.QueueName} queue: {joinResult.Reason} ({joinResult.Code})");
                        Error?.Invoke(this, exception);
                        ErrorAction?.Invoke(exception);
                        return;
                    }
                }
            }

            foreach (ChannelSubscriberRegistration registration in Channel.Registrations)
            {
                HorseResult joinResult = await Channel.Subscribe(registration.Name, true);
                if (joinResult.Code == HorseResultCode.Ok)
                    continue;

                if (DisconnectionOnAutoJoinFailure)
                {
                    if (_socket != null)
                    {
                        _socket.Disconnect();
                        _socket = null;

                        HorseChannelException exception = new($"Can't subscribe to {registration.Name} channel: {joinResult.Reason} ({joinResult.Code})");
                        Error?.Invoke(this, exception);
                        ErrorAction?.Invoke(exception);
                        return;
                    }
                }
            }

            foreach (EventSubscriberRegistration registration in Event.Registrations)
            {
                HorseResult joinResult = await Event.Subscribe(registration.Type, registration.Target);
                if (joinResult.Code == HorseResultCode.Ok)
                    continue;

                if (DisconnectionOnAutoJoinFailure)
                {
                    if (_socket != null)
                    {
                        _socket.Disconnect();
                        _socket = null;

                        HorseChannelException exception = new($"Can't subscribe to {registration.Type} event: {joinResult.Reason} ({joinResult.Code})");
                        Error?.Invoke(this, exception);
                        ErrorAction?.Invoke(exception);
                        return;
                    }
                }
            }
        }

        internal void OnDisconnected()
        {
            Tracker.MarkAllMessagesExpired();
            Disconnected?.Invoke(this);
            DisconnectedAction?.Invoke(this);
        }
    }
}