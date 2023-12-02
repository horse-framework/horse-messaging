using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Client.Queues.Internal;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client;

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
    /// Defined remote hosts.
    /// If Connect methods are used with remote host parameter, this value is updated.
    /// </summary>
    internal List<string> RemoteHosts { get; } = new();

    private int _hostIndex = -1;

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
    /// If true, throws all exceptions about connections.
    /// Error event handler always is triggered even exception thrown or not.
    /// Default value is false.
    /// </summary>
    public bool ThrowExceptions { get; set; } = false;

    /// <summary>
    /// Setting true disables Nagle Algorithm
    /// </summary>
    public bool? NoDelay { get; set; }

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
    public IMessageContentSerializer MessageSerializer { get; set; } = new SystemJsonContentSerializer();

    internal IServiceProvider Provider { get; set; }

    /// <summary>
    /// Switching protocol
    /// </summary>
    public ISwitchingProtocol SwitchingProtocol { get; set; }

    #endregion

    #region Constructors - Destructors

    private TimeSpan _reconnectWait = TimeSpan.FromSeconds(3);
    private HorseSocket _socket;
    private readonly ConnectionData _data = new();
    private bool _autoConnect;
    private Timer _reconnectTimer;

    static HorseClient()
    {
        SerializerFactory.AddConverter(new EnumConverter<PutBack>());
        SerializerFactory.AddConverter(new EnumConverter<MessagingQueueType>());
    }

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
            _reconnectTimer = new Timer(_ =>
            {
                if (!_autoConnect || IsConnected)
                    return;

                if (_socket != null)
                {
                    if (_socket.IsConnecting)
                        return;

                    _socket.Disconnect();
                }

                Connect(FindNextTargetHost());
            }, null, ms, ms);
        }
    }

    /// <summary>
    /// Adds new remote host to connect
    /// </summary>
    /// <param name="hostname">Host name with protocol horse://... or horses://...</param>
    public void AddHost(string hostname)
    {
        if (string.IsNullOrEmpty(hostname))
            throw new Exception("Remote host name is empty");

        lock (RemoteHosts)
        {
            if (!RemoteHosts.Contains(hostname, StringComparer.InvariantCultureIgnoreCase))
                RemoteHosts.Add(hostname);
        }
    }

    private string FindNextTargetHost()
    {
        try
        {
            lock (RemoteHosts)
            {
                if (RemoteHosts.Count == 0)
                    throw new Exception("There is no host to connect");

                _hostIndex++;
                if (_hostIndex >= RemoteHosts.Count)
                    _hostIndex = 0;

                return RemoteHosts[_hostIndex];
            }
        }
        catch (Exception e)
        {
            OnException(e);

            if (ThrowExceptions)
                throw;

            return null;
        }
    }

    private void RefreshRemoteHosts(HorseMessage message)
    {
        string successorHost = message.FindHeader(HorseHeaders.SUCCESSOR_NODE);
        string replaceNodes = message.FindHeader(HorseHeaders.REPLICA_NODE);

        lock (RemoteHosts)
        {
            string mainHost = RemoteHosts[_hostIndex];

            if (!string.IsNullOrEmpty(replaceNodes))
            {
                string[] replicaHosts = replaceNodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (string replicaHost in replicaHosts)
                {
                    if (string.IsNullOrEmpty(replicaHost))
                        continue;

                    if (!RemoteHosts.Contains(replicaHost))
                        RemoteHosts.Add(replicaHost);
                }
            }

            if (!string.IsNullOrEmpty(successorHost))
            {
                RemoteHosts.Remove(successorHost);
                RemoteHosts.Insert(0, successorHost);
            }

            RemoteHosts.Remove(mainHost);
            RemoteHosts.Insert(0, mainHost);
            _hostIndex = -1;
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
        Connect(FindNextTargetHost());
    }

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    public void Connect(string host)
    {
        try
        {
            if (string.IsNullOrEmpty(host))
                throw new Exception("Remote host name is empty");

            lock (RemoteHosts)
                if (!RemoteHosts.Contains(host, StringComparer.InvariantCultureIgnoreCase))
                    RemoteHosts.Add(host);

            SetAutoReconnect(true);

            DnsResolver resolver = new DnsResolver();
            DnsInfo info = resolver.Resolve(host);
            Connect(info);
        }
        catch (Exception e)
        {
            OnException(e);

            if (ThrowExceptions)
                throw;
        }
    }

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    private void Connect(DnsInfo host)
    {
        if (string.IsNullOrEmpty(_clientId))
            _clientId = UniqueIdGenerator.Create();

        /*
        if (host.Protocol != Core.Protocol.Hmq)
            throw new NotSupportedException("Only Horse protocol is supported");
            */

        try
        {
            _data.Properties["Host"] = host.Hostname;
            _socket = new HorseSocket(this, _data);
            _socket.Connect(host);
        }
        catch (Exception e)
        {
            OnException(e);

            if (ThrowExceptions)
                throw;
        }
    }

    /// <summary>
    /// Connects to RemoteHost
    /// </summary>
    public Task ConnectAsync()
    {
        return ConnectAsync(FindNextTargetHost());
    }

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    public Task ConnectAsync(string host)
    {
        try
        {
            if (string.IsNullOrEmpty(host))
                throw new Exception("Remote host name is empty");

            lock (RemoteHosts)
                if (!RemoteHosts.Contains(host, StringComparer.InvariantCultureIgnoreCase))
                    RemoteHosts.Add(host);

            SetAutoReconnect(true);

            DnsResolver resolver = new DnsResolver();
            DnsInfo info = resolver.Resolve(host);
            return ConnectAsync(info);
        }
        catch (Exception e)
        {
            OnException(e);

            if (ThrowExceptions)
                throw;

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    private async Task ConnectAsync(DnsInfo host)
    {
        if (string.IsNullOrEmpty(_clientId))
            _clientId = UniqueIdGenerator.Create();

        try
        {
            _data.Properties["Host"] = host.Hostname;
            _socket = new HorseSocket(this, _data);
            await _socket.ConnectAsync(host);
        }
        catch (Exception e)
        {
            OnException(e);

            if (ThrowExceptions)
                throw;
        }
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
    /// Sends raw byte array over socket
    /// </summary>
    public bool SendRaw(byte[] data)
    {
        return _socket.Send(data);
    }

    /// <summary>
    /// Sends raw byte array over socket
    /// </summary>
    public Task<bool> SendRawAsync(byte[] data)
    {
        return _socket.SendAsync(data);
    }

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

        if (_socket == null)
            return new HorseResult(HorseResultCode.SendError);

        bool sent;
        if (SwitchingProtocol != null)
            sent = await SwitchingProtocol.SendAsync(message, additionalHeaders);
        else
        {
            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);
            sent = await _socket.SendAsync(data);
        }

        return sent ? HorseResult.Ok() : new HorseResult(HorseResultCode.SendError);
    }

    /// <summary>
    /// Sends multiple messages
    /// </summary>
    public void Send(HorseMessage message, Action<bool> sendCallback)
    {
        if (_socket == null)
        {
            sendCallback(false);
            return;
        }

        message.SetSource(ClientId);

        if (string.IsNullOrEmpty(message.MessageId))
            message.SetMessageId(UniqueIdGenerator.Create());

        if (SwitchingProtocol != null)
            SwitchingProtocol.Send(message);
        else
        {
            byte[] data = HorseProtocolWriter.Create(message);
            _socket.Send(data, sendCallback);
        }
    }

    /// <summary>
    /// Sends multiple messages
    /// </summary>
    public void SendBulk(IEnumerable<HorseMessage> messages, Action<HorseMessage, bool> sendCallback)
    {
        if (_socket == null)
        {
            foreach (HorseMessage message in messages)
                sendCallback?.Invoke(message, false);

            return;
        }

        _socket.SendBulk(messages, sendCallback);
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

        if (string.IsNullOrEmpty(message.MessageId))
            message.SetMessageId(UniqueIdGenerator.Create());

        Task<HorseMessage> task = Tracker.Track(message);

        HorseResult sent = await SendAsync(message);
        if (sent.Code != HorseResultCode.Ok)
        {
            Tracker.Forget(message);
            return message.CreateResponse(sent.Code);
        }

        HorseMessage response = await task ?? message.CreateResponse(HorseResultCode.RequestTimeout);

        return response;
    }

    #endregion

    #region Acknowledge - Response

    /// <summary>
    /// Sends negative acknowledge message for the message.
    /// </summary>
    public async Task<HorseResult> SendNegativeAck(HorseMessage message, string reason = null)
    {
        if (string.IsNullOrEmpty(reason))
            reason = HorseHeaders.NACK_REASON_NONE;

        HorseMessage ack = message.CreateAcknowledge(reason);
        return await SendAsync(ack);
    }

    /// <summary>
    /// Sends acknowledge message for the message.
    /// </summary>
    public async Task<HorseResult> SendAck(HorseMessage message)
    {
        HorseMessage ack = message.CreateAcknowledge();
        return await SendAsync(ack);
    }

    /// <summary>
    /// Sends success response for the message
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task<HorseResult> SendResponse(HorseMessage message)
    {
        return SendAck(message);
    }

    /// <summary>
    /// /// Sends negative response for the message
    /// </summary>
    /// <param name="message">Received horse message</param>
    /// <param name="reason">Description for the error</param>
    /// <returns></returns>
    public Task<HorseResult> SendNegativeResponse(HorseMessage message, string reason = null)
    {
        return SendNegativeAck(message, reason);
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

                if (message.ContentType == KnownContentTypes.Found)
                {
                    string mainHost = message.FindHeader(HorseHeaders.NODE_PUBLIC_HOST);
                    string successorHost = message.FindHeader(HorseHeaders.SUCCESSOR_NODE);

                    lock (RemoteHosts)
                    {
                        if (!string.IsNullOrEmpty(successorHost) && !RemoteHosts.Contains(successorHost))
                            RemoteHosts.Add(successorHost);

                        if (!string.IsNullOrEmpty(mainHost) && !RemoteHosts.Contains(mainHost))
                            RemoteHosts.Add(mainHost);
                    }
                }

                else if (message.ContentType == KnownContentTypes.Accepted || message.ContentType == KnownContentTypes.ResetContent)
                    RefreshRemoteHosts(message);

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
            QueueTypeDescriptor modelDescriptor = Queue.DescriptorContainer.GetDescriptor(registration.MessageType);
            QueueTypeDescriptor consumerDescriptor = Queue.DescriptorContainer.GetDescriptor(registration.ConsumerType);

            List<KeyValuePair<string, string>> descriptorHeaders = new List<KeyValuePair<string, string>>();

            if (modelDescriptor != null)
            {
                HorseMessage modelMessage = modelDescriptor.CreateMessage();
                if (modelMessage.HasHeader)
                    descriptorHeaders.AddRange(modelMessage.Headers);
            }

            if (consumerDescriptor != null)
            {
                HorseMessage consumerMessage = consumerDescriptor.CreateMessage();
                if (consumerMessage.HasHeader)
                {
                    foreach (KeyValuePair<string, string> pair in consumerMessage.Headers)
                    {
                        descriptorHeaders.RemoveAll(x => x.Key == pair.Key);
                        descriptorHeaders.Add(pair);
                    }
                }
            }

            HorseResult joinResult = await Queue.Subscribe(registration.QueueName, true, descriptorHeaders);
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