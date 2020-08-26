using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations.Resolvers;
using Twino.Client.TMQ.Internal;
using Twino.Client.TMQ.Models;
using Twino.Client.TMQ.Operators;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// TMQ Client class
    /// Can be used directly with event subscriptions
    /// Or can be base class to a derived Client class and provides virtual methods for all events
    /// </summary>
    public class TmqClient : ClientSocketBase<TwinoMessage>, IDisposable
    {
        #region Properties

        /// <summary>
        /// Unique Id generator for sending messages
        /// </summary>
        public IUniqueIdGenerator UniqueIdGenerator { get; set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// If true, each message has it's own unique message id.
        /// IF false, message unique id value will be sent as empty.
        /// Default value is true
        /// </summary>
        public bool UseUniqueMessageId { get; set; } = true;

        /// <summary>
        /// If true, acknowledge message will be sent automatically if message requires.
        /// </summary>
        public bool AutoAcknowledge { get; set; }

        /// <summary>
        /// If true, acknowledge messages will trigger on message received events.
        /// If false, acknowledge messages are proceed silently.
        /// </summary>
        public bool CatchAcknowledgeMessages { get; set; }

        /// <summary>
        /// If true, response messages will trigger on message received events.
        /// If false, response messages are proceed silently.
        /// </summary>
        public bool CatchResponseMessages { get; set; }

        /// <summary>
        /// Maximum time to wait acknowledge message
        /// </summary>
        public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(15);

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
        /// Acknowledge and Response message follower of the client
        /// </summary>
        private readonly MessageFollower _follower;

        /// <summary>
        /// TMQ Client Direct message management object
        /// </summary>
        public DirectOperator Direct { get; }

        /// <summary>
        /// TMQ Client Channel Management object
        /// </summary>
        public ChannelOperator Channels { get; }

        /// <summary>
        /// TMQ Client Queue Management object
        /// </summary>
        public QueueOperator Queues { get; }

        /// <summary>
        /// TMQ Client Connection Management object
        /// </summary>
        public ConnectionOperator Connections { get; }

        /// <summary>
        /// TMQ Client Router Management object
        /// </summary>
        public RouterOperator Routers { get; }

        /// <summary>
        /// Event manage of the client
        /// </summary>
        internal EventManager Events { get; set; }

        /// <summary>
        /// Type delivery container
        /// </summary>
        internal ITypeDeliveryContainer DeliveryContainer { get; set; }

        /// <summary>
        /// Serializer object for JSON messages
        /// </summary>
        public IMessageContentSerializer JsonSerializer { get; set; } = new NewtonsoftContentSerializer();

        #endregion

        #region Constructors - Destructors

        /// <summary>
        /// Creates new TMQ protocol client
        /// </summary>
        public TmqClient()
        {
            Data.Method = "CONNECT";
            Data.Path = "/";

            Direct = new DirectOperator(this);
            Channels = new ChannelOperator(this);
            Queues = new QueueOperator(this);
            Connections = new ConnectionOperator(this);
            Routers = new RouterOperator(this);

            Events = new EventManager();
            DeliveryContainer = new TypeDeliveryContainer(new TypeDeliveryResolver());

            _follower = new MessageFollower(this);
            _follower.Run();
        }

        /// <summary>
        /// Releases all resources of the client
        /// </summary>
        public void Dispose()
        {
            _follower?.Dispose();
            Queues.Dispose();
        }

        #endregion

        #region Connection Data

        /// <summary>
        /// Sets client type information of the client
        /// </summary>
        public void SetClientType(string type)
        {
            if (Data.Properties.ContainsKey(TwinoHeaders.CLIENT_TYPE))
                Data.Properties[TwinoHeaders.CLIENT_TYPE] = type;
            else
                Data.Properties.Add(TwinoHeaders.CLIENT_TYPE, type);
        }

        /// <summary>
        /// Sets client name information of the client
        /// </summary>
        public void SetClientName(string name)
        {
            if (Data.Properties.ContainsKey(TwinoHeaders.CLIENT_NAME))
                Data.Properties[TwinoHeaders.CLIENT_NAME] = name;
            else
                Data.Properties.Add(TwinoHeaders.CLIENT_NAME, name);
        }

        /// <summary>
        /// Sets client token information of the client
        /// </summary>
        public void SetClientToken(string token)
        {
            if (Data.Properties.ContainsKey(TwinoHeaders.CLIENT_TOKEN))
                Data.Properties[TwinoHeaders.CLIENT_TOKEN] = token;
            else
                Data.Properties.Add(TwinoHeaders.CLIENT_TOKEN, token);
        }

        #endregion

        #region Connect - Read

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public override void Connect(DnsInfo host)
        {
            if (string.IsNullOrEmpty(_clientId))
                _clientId = UniqueIdGenerator.Create();

            if (host.Protocol != Protocol.Tmq)
                throw new NotSupportedException("Only TMQ protocol is supported");

            try
            {
                Client = new TcpClient();
                Client.Connect(host.IPAddress, host.Port);
                IsConnected = true;
                IsSsl = host.SSL;

                //creates SSL Stream or Insecure stream
                if (host.SSL)
                {
                    SslStream sslStream = new SslStream(Client.GetStream(), true, CertificateCallback);

                    X509Certificate2Collection certificates = null;
                    if (Certificate != null)
                    {
                        certificates = new X509Certificate2Collection();
                        certificates.Add(Certificate);
                    }

                    sslStream.AuthenticateAsClient(host.Hostname, certificates, false);
                    Stream = sslStream;
                }
                else
                    Stream = Client.GetStream();

                Stream.Write(PredefinedMessages.PROTOCOL_BYTES_V2);
                SendInfoMessage(host).Wait();

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES_V2.Length];
                int len = Stream.Read(buffer, 0, buffer.Length);

                CheckProtocolResponse(buffer, len);

                Start();
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public override async Task ConnectAsync(DnsInfo host)
        {
            if (string.IsNullOrEmpty(_clientId))
                _clientId = UniqueIdGenerator.Create();

            if (host.Protocol != Protocol.Tmq)
                throw new NotSupportedException("Only TMQ protocol is supported");

            try
            {
                Client = new TcpClient();
                await Client.ConnectAsync(host.IPAddress, host.Port);
                IsConnected = true;
                IsSsl = host.SSL;

                //creates SSL Stream or Insecure stream
                if (host.SSL)
                {
                    SslStream sslStream = new SslStream(Client.GetStream(), true, CertificateCallback);

                    X509Certificate2Collection certificates = null;
                    if (Certificate != null)
                    {
                        certificates = new X509Certificate2Collection();
                        certificates.Add(Certificate);
                    }

                    await sslStream.AuthenticateAsClientAsync(host.Hostname, certificates, false);
                    Stream = sslStream;
                }
                else
                    Stream = Client.GetStream();

                await Stream.WriteAsync(PredefinedMessages.PROTOCOL_BYTES_V2);
                await SendInfoMessage(host);

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES_V2.Length];
                int len = await Stream.ReadAsync(buffer, 0, buffer.Length);

                CheckProtocolResponse(buffer, len);

                Start();
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        /// <summary>
        /// Checks protocol response message from server.
        /// If protocols are not matched, an exception is thrown 
        /// </summary>
        private static void CheckProtocolResponse(byte[] buffer, int length)
        {
            if (length < PredefinedMessages.PROTOCOL_BYTES_V2.Length)
                throw new InvalidOperationException("Unexpected server response");

            for (int i = 0; i < PredefinedMessages.PROTOCOL_BYTES_V2.Length; i++)
                if (PredefinedMessages.PROTOCOL_BYTES_V2[i] != buffer[i])
                    throw new NotSupportedException("Unsupported TMQ Protocol version. Server supports: " + Encoding.UTF8.GetString(buffer));
        }

        /// <summary>
        /// Startes to read messages from server
        /// </summary>
        private void Start()
        {
            //fire connected events and start to read data from the server until disconnected
            Thread thread = new Thread(async () =>
            {
                try
                {
                    while (IsConnected)
                        await Read();
                }
                catch
                {
                    Disconnect();
                }
            });

            thread.IsBackground = true;
            thread.Start();

            OnConnected();
        }

        /// <summary>
        /// Sends connection data message to server, called right after procotol handshaking completed.
        /// </summary>
        /// <returns></returns>
        private async Task SendInfoMessage(DnsInfo dns)
        {
            if (Data?.Properties == null ||
                Data.Properties.Count < 1 &&
                string.IsNullOrEmpty(Data.Method) &&
                string.IsNullOrEmpty(Data.Path) &&
                string.IsNullOrEmpty(ClientId))
                return;

            TwinoMessage message = new TwinoMessage();
            message.FirstAcquirer = true;
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Hello;

            message.Content = new MemoryStream();

            Data.Path = string.IsNullOrEmpty(dns.Path) ? "/" : dns.Path;
            if (string.IsNullOrEmpty(Data.Method))
                Data.Method = "NONE";

            string first = Data.Method + " " + Data.Path + "\r\n";
            await message.Content.WriteAsync(Encoding.UTF8.GetBytes(first));

            if (Data.Properties.ContainsKey(TwinoHeaders.CLIENT_ID))
                Data.Properties[TwinoHeaders.CLIENT_ID] = ClientId;
            else
                Data.Properties.Add(TwinoHeaders.CLIENT_ID, ClientId);

            foreach (var prop in Data.Properties)
            {
                string line = prop.Key + ": " + prop.Value + "\r\n";
                byte[] lineData = Encoding.UTF8.GetBytes(line);
                await message.Content.WriteAsync(lineData);
            }

            await SendAsync(message);
        }

        /// <summary>
        /// Reads messages from server
        /// </summary>
        /// <returns></returns>
        protected override async Task Read()
        {
            TmqReader reader = new TmqReader();
            TwinoMessage message = await reader.Read(Stream);

            if (message == null)
            {
                Disconnect();
                return;
            }

            if (message.Ttl < 0)
                return;

            if (SmartHealthCheck)
                KeepAlive();

            switch (message.Type)
            {
                case MessageType.Server:
                    if (message.ContentType == KnownContentTypes.Accepted)
                        _clientId = message.Target;
                    break;

                case MessageType.Terminate:
                    Disconnect();
                    break;

                case MessageType.Pong:
                    KeepAlive();
                    break;

                case MessageType.Ping:
                    Pong();
                    break;

                case MessageType.Acknowledge:
                    _follower.ProcessAcknowledge(message);

                    if (CatchAcknowledgeMessages)
                        SetOnMessageReceived(message);
                    break;

                case MessageType.Response:
                    _follower.ProcessResponse(message);

                    if (CatchResponseMessages)
                        SetOnMessageReceived(message);
                    break;

                case MessageType.Event:
                    _ = Events.TriggerEvents(this, message);
                    break;

                case MessageType.QueueMessage:

                    if (message.PendingAcknowledge && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    //if message is response for pull request, process pull container
                    if (Queues.PullContainers.Count > 0 && message.HasHeader)
                    {
                        string requestId = message.FindHeader(TwinoHeaders.REQUEST_ID);
                        if (!string.IsNullOrEmpty(requestId))
                        {
                            PullContainer container;
                            lock (Queues.PullContainers)
                                Queues.PullContainers.TryGetValue(requestId, out container);

                            if (container != null)
                            {
                                ProcessPull(requestId, message, container);
                                break;
                            }
                        }
                    }

                    SetOnMessageReceived(message);
                    break;


                case MessageType.DirectMessage:
                    if (message.PendingAcknowledge && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    SetOnMessageReceived(message);
                    break;
            }
        }

        /// <summary>
        /// Processes pull message
        /// </summary>
        private void ProcessPull(string requestId, TwinoMessage message, PullContainer container)
        {
            if (message.Length > 0)
            {
                container.AddMessage(message);
                return;
            }

            string noContent = message.FindHeader(TwinoHeaders.NO_CONTENT);

            if (!string.IsNullOrEmpty(noContent))
            {
                lock (Queues.PullContainers)
                    Queues.PullContainers.Remove(requestId);

                container.Complete(noContent);
            }
        }

        /// <summary>
        /// Client disconnected from the server
        /// </summary>
        protected override void OnDisconnected()
        {
            base.OnDisconnected();
            _follower.MarkAllMessagesExpired();
        }

        #endregion

        #region Ping - Pong

        /// <summary>
        /// Sends a PING message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends a PONG message
        /// </summary>
        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        #endregion

        #region Send

        /// <summary>
        /// Sends a TMQ message
        /// </summary>
        public bool Send(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = TmqWriter.Create(message, additionalHeaders);
            return Send(data);
        }

        /// <summary>
        /// Sends a TMQ message
        /// </summary>
        public async Task<TwinoResult> SendAsync(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = TmqWriter.Create(message, additionalHeaders);
            bool sent = await SendAsync(data);
            return sent ? TwinoResult.Ok() : new TwinoResult(TwinoResultCode.SendError);
        }

        /// <summary>
        /// Sends a TMQ message and waits for acknowledge
        /// </summary>
        public async Task<TwinoResult> SendWithAcknowledge(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);
            message.PendingAcknowledge = true;
            message.WaitResponse = false;
            message.SetMessageId(UniqueIdGenerator.Create());
            
            if (additionalHeaders!=null)
                foreach (KeyValuePair<string, string> pair in additionalHeaders)
                    message.AddHeader(pair.Key, pair.Value);
                    
            if (string.IsNullOrEmpty(message.MessageId))
                throw new ArgumentNullException("Messages without unique id cannot be acknowledged");

            return await SendAndWaitForAcknowledge(message, true);
        }

        /// <summary>
        /// Sends a message, waits response and deserializes JSON response to T template type
        /// </summary>
        public async Task<TmqModelResult<T>> SendAndGetJson<T>(TwinoMessage message)
        {
            message.WaitResponse = true;

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            Task<TwinoMessage> task = _follower.FollowResponse(message);
            TwinoResult sent = await SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
                return new TmqModelResult<T>(new TwinoResult(TwinoResultCode.SendError));

            TwinoMessage response = await task;
            if (response?.Content == null || response.Length == 0 || response.Content.Length == 0)
                return TmqModelResult<T>.FromContentType(message.ContentType);

            T model = response.Deserialize<T>(JsonSerializer);
            return new TmqModelResult<T>(TwinoResult.Ok(), model);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<TwinoResult> SendResponseAsync<TModel>(TwinoMessage requestMessage, TModel responseModel)
        {
            TwinoMessage response = requestMessage.CreateResponse(TwinoResultCode.Ok);
            response.Serialize(responseModel, JsonSerializer);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<TwinoResult> SendResponseAsync(TwinoMessage requestMessage, string responseContent)
        {
            TwinoMessage response = requestMessage.CreateResponse(TwinoResultCode.Ok);
            response.SetStringContent(responseContent);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<TwinoResult> SendResponseAsync(TwinoMessage requestMessage, Stream content)
        {
            TwinoMessage response = requestMessage.CreateResponse(TwinoResultCode.Ok);
            response.Content = new MemoryStream();
            await content.CopyToAsync(response.Content);
            return await SendAsync(response);
        }

        /// <summary>
        /// Sends the message and waits for response
        /// </summary>
        public async Task<TwinoMessage> Request(TwinoMessage message)
        {
            message.WaitResponse = true;
            message.PendingAcknowledge = false;
            message.SetMessageId(UniqueIdGenerator.Create());

            Task<TwinoMessage> task = _follower.FollowResponse(message);

            TwinoResult sent = await SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
            {
                _follower.UnfollowMessage(message);
                return message.CreateResponse(sent.Code);
            }

            TwinoMessage response = await task;
            if (response == null)
                response = message.CreateResponse(TwinoResultCode.RequestTimeout);
            
            return response;
        }

        #endregion

        #region Acknowledge - Response

        /// <summary>
        /// Sends unacknowledge message for the message.
        /// </summary>
        public async Task<TwinoResult> SendNegativeAck(TwinoMessage message, string reason = null)
        {
            if (!message.PendingAcknowledge)
                return new TwinoResult(TwinoResultCode.Unacceptable);

            if (string.IsNullOrEmpty(reason))
                reason = TwinoHeaders.NACK_REASON_NONE;

            TwinoMessage ack = message.CreateAcknowledge(reason);
            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends unacknowledge message for the message.
        /// </summary>
        public async Task<TwinoResult> SendAck(TwinoMessage message)
        {
            if (!message.PendingAcknowledge)
                return new TwinoResult(TwinoResultCode.Unacceptable);

            TwinoMessage ack = message.CreateAcknowledge();
            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends message.
        /// if verify requires, waits response and checkes status code of the response.
        /// returns true if Ok.
        /// </summary>
        protected internal async Task<TwinoResult> WaitResponse(TwinoMessage message, bool waitForResponse)
        {
            Task<TwinoMessage> task = null;
            if (waitForResponse)
                task = _follower.FollowResponse(message);

            TwinoResult sent = await SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
            {
                if (waitForResponse)
                    _follower.UnfollowMessage(message);

                return new TwinoResult(TwinoResultCode.SendError);
            }

            if (waitForResponse)
            {
                TwinoMessage response = await task;
                return response == null
                           ? TwinoResult.Failed()
                           : new TwinoResult((TwinoResultCode) response.ContentType);
            }

            return TwinoResult.Ok();
        }

        /// <summary>
        /// Sends message.
        /// if acknowledge is pending, waits for acknowledge.
        /// returns true if received.
        /// </summary>
        internal async Task<TwinoResult> SendAndWaitForAcknowledge(TwinoMessage message, bool waitAcknowledge)
        {
            Task<TwinoResult> task = null;
            if (waitAcknowledge)
                task = _follower.FollowAcknowledge(message);

            TwinoResult sent = await SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
            {
                if (waitAcknowledge)
                    _follower.UnfollowMessage(message);

                return new TwinoResult(TwinoResultCode.SendError);
            }

            if (waitAcknowledge)
                return await task;

            return TwinoResult.Ok();
        }

        #endregion

        #region Events

        internal async Task<bool> EventSubscription(string eventName, bool subscribe, string channelName, ushort? queueId)
        {
            ushort ct = subscribe ? (ushort) 1 : (ushort) 0;
            TwinoMessage message = new TwinoMessage(MessageType.Event, eventName, ct);
            message.SetMessageId(UniqueIdGenerator.Create());
            message.WaitResponse = true;

            if (!string.IsNullOrEmpty(channelName))
                message.AddHeader(TwinoHeaders.CHANNEL_NAME, channelName);

            if (queueId.HasValue)
                message.AddHeader(TwinoHeaders.QUEUE_ID, queueId.Value.ToString());

            Task<TwinoMessage> task = _follower.FollowResponse(message);

            TwinoResult sent = await SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
            {
                _follower.UnfollowMessage(message);
                return false;
            }

            TwinoMessage response = await task;
            if (response == null)
                return false;

            TwinoResultCode code = (TwinoResultCode) response.ContentType;
            return code == TwinoResultCode.Ok;
        }

        #endregion
    }
}