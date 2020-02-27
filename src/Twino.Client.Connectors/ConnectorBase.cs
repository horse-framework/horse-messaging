using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core;

namespace Twino.Client.Connectors
{
    /// <summary>
    /// When an excaption thrown in connector lifecycle and event is fired.
    /// The event's delegate is this delegate.
    /// </summary>
    public delegate void ConnectorExceptionHandler<in TClient, TMessage>(IConnector<TClient, TMessage> connector, Exception ex)
        where TClient : ClientSocketBase<TMessage>, new();

    /// <summary>
    /// Function definition for client message received event
    /// </summary>
    public delegate void ConnectorMessageHandler<in TClient, in TMessage>(TClient client, TMessage message)
        where TClient : ClientSocketBase<TMessage>, new();

    /// <summary>
    /// Function definition for client connection and disconnection events
    /// </summary>
    public delegate void ConnectorConnectionHandler<in TClient>(TClient client)
        where TClient : SocketBase, new();

    /// <summary>
    /// Base class for all connectors
    /// </summary>
    public abstract class ConnectorBase<TClient, TMessage> : IConnector<TClient, TMessage>
        where TClient : ClientSocketBase<TMessage>, new()
    {
        #region Properties

        /// <summary>
        /// Running status
        /// </summary>
        protected bool _running;

        /// <summary>
        /// Current client instance.
        /// </summary>
        private TClient _client;

        /// <summary>
        /// Connection data and properties for the client
        /// </summary>
        private readonly ConnectionData _data = new ConnectionData();

        /// <summary>
        /// Next hostname in the host list
        /// </summary>
        private int _hostIndex;

        private DateTime _lastConnection = DateTime.UtcNow;
        private int _connectionCount;

        /// <summary>
        /// Host list
        /// </summary>
        private readonly List<string> _hosts;

        /// <summary>
        /// Client connected event handler
        /// </summary>
        public event ConnectorConnectionHandler<TClient> Connected;

        /// <summary>
        /// Client disconnected event handler
        /// </summary>
        public event ConnectorConnectionHandler<TClient> Disconnected;

        /// <summary>
        /// Message received from server handler
        /// </summary>
        public event ConnectorMessageHandler<TClient, TMessage> MessageReceived;

        /// <summary>
        /// Exception thrown event handler in tcp operations
        /// </summary>
        public event ConnectorExceptionHandler<TClient, TMessage> ExceptionThrown;

        /// <summary>
        /// If true, connector is connected to specified host
        /// </summary>
        public bool IsConnected => _client != null && _client.IsConnected;

        /// <summary>
        /// Returns how much time past until last connection established.
        /// If there is no active connection, returns TimeSpan.Zero
        /// </summary>
        public TimeSpan Lifetime => IsConnected ? (DateTime.UtcNow - _lastConnection) : TimeSpan.Zero;

        /// <summary>
        /// Returns how many times connection established.
        /// Created for using to monitor if the connector disconnects often or not
        /// </summary>
        public int ConnectionCount => _connectionCount;

        /// <summary>
        /// Returns connector status
        /// </summary>
        public bool IsRunning => _running;

        /// <summary>
        /// Called to create new instance of the client.
        /// If null, default constructor will be used.
        /// </summary>
        private readonly Func<TClient> _createInstance;

        /// <summary>
        /// Gets currently active client object of connector
        /// </summary>
        /// <returns></returns>
        public TClient GetClient()
        {
            return _client;
        }

        /// <summary>
        /// User-defined tag object for the connector
        /// </summary>
        public object Tag { get; set; }

        #endregion

        /// <summary>
        /// Creates new connector base
        /// </summary>
        protected ConnectorBase()
            : this(null)
        {
        }

        /// <summary>
        /// Creates new connector base with Client instance creation action
        /// </summary>
        protected ConnectorBase(Func<TClient> createInstance)
        {
            _createInstance = createInstance;
            _hosts = new List<string>();
        }

        #region Headers

        /// <summary>
        /// Adds a host to remote hosts list
        /// </summary>
        public void AddHost(string host)
        {
            lock (_hosts)
                _hosts.Add(host);
        }

        /// <summary>
        /// Removes the host from remote hosts list
        /// </summary>
        public void RemoveHost(string host)
        {
            lock (_hosts)
                _hosts.Remove(host);
        }

        /// <summary>
        /// Clear all hosts in remote hosts list
        /// </summary>
        public void ClearHosts()
        {
            lock (_hosts)
                _hosts.Clear();

            _hostIndex = 0;
        }

        /// <summary>
        /// Add a new custom property.
        /// If the property is already exists, it will be changed.
        /// </summary>
        public void AddProperty(string key, string value)
        {
            lock (_data)
            {
                if (_data.Properties.ContainsKey(key))
                    _data.Properties[key] = value;
                else
                    _data.Properties.Add(key, value);
            }
        }

        /// <summary>
        /// Removes a custom property
        /// </summary>
        public void RemoveProperty(string key)
        {
            lock (_data)
                _data.Properties.Remove(key);
        }

        /// <summary>
        /// Clears all custom properties
        /// </summary>
        public void ClearProperties()
        {
            lock (_data)
                _data.Properties.Clear();
        }

        #endregion

        #region Actions

        /// <summary>
        /// Runs the connector
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// Stops the connector
        /// </summary>
        public abstract void Abort();

        /// <summary>
        /// Connects to the host. Throws exception on error.
        /// New instance is created for Client.
        /// </summary>
        protected void Connect()
        {
            Disconnect();

            _client = _createInstance != null
                          ? _createInstance()
                          : new TClient();

            if (_data != null && _data.Properties != null)
                lock (_data)
                    foreach (var kv in _data.Properties)
                        _client.Data.Properties.Add(kv.Key, kv.Value);

            _client.Connected += ClientConnected;
            _client.Disconnected += ClientDisconnected;
            _client.MessageReceived += ClientMessageReceived;

            string host;
            lock (_hosts)
            {
                if (_hosts.Count == 0)
                    throw new InvalidOperationException("Connector needs a host to connect");


                if (_hostIndex >= _hosts.Count)
                    _hostIndex = 0;

                host = _hosts[_hostIndex];
                _hostIndex++;
            }

            _client.Connect(host);
        }

        /// <summary>
        /// Disconnects from the server and dispose Client.
        /// </summary>
        protected void Disconnect()
        {
            if (_client == null)
                return;

            if (_client.IsConnected)
                _client.Disconnect();

            _client.Connected -= ClientConnected;
            _client.Disconnected -= ClientDisconnected;
            _client.MessageReceived -= ClientMessageReceived;

            _client = null;
        }

        /// <summary>
        /// Checks if the client is null or disconnected.
        /// Returns true if not connected
        /// </summary>
        /// <returns></returns>
        protected bool NeedReconnect()
        {
            if (_client == null)
                return true;

            if (!_client.IsConnected)
                return true;

            return false;
        }

        /// <summary>
        /// Raises exception event
        /// </summary>
        protected void RaiseException(Exception ex)
        {
            ExceptionThrown?.Invoke(this, ex);
        }

        #endregion

        #region Client Events

        /// <summary>
        /// Raises client message received event
        /// </summary>
        protected virtual void ClientMessageReceived(ClientSocketBase<TMessage> client, TMessage payload)
        {
            MessageReceived?.Invoke((TClient)client, payload);
        }

        /// <summary>
        /// Raises client disconnected event
        /// </summary>
        protected virtual void ClientDisconnected(SocketBase client)
        {
            Disconnected?.Invoke((TClient)client);
        }

        /// <summary>
        /// Raises client connected event
        /// </summary>
        protected virtual void ClientConnected(SocketBase client)
        {
            _connectionCount++;
            _lastConnection = DateTime.UtcNow;

            Connected?.Invoke((TClient)client);
        }

        /// <summary>
        /// Sends the message to the server
        /// </summary>
        public virtual bool Send(byte[] data)
        {
            return _client != null && _client.Send(data);
        }

        /// <summary>
        /// Sends the message to the server.
        /// </summary>
        public virtual async Task<bool> SendAsync(byte[] data)
        {
            return _client != null && await _client.SendAsync(data);
        }

        #endregion
    }
}