using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Mq.Client.Annotations.Resolvers;
using Horse.Mq.Client.Internal;
using Horse.Mq.Client.Models;
using Horse.Mq.Client.Operators;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client
{
    /// <summary>
    /// HMQ Client class
    /// Can be used directly with event subscriptions
    /// Or can be base class to a derived Client class and provides virtual methods for all events
    /// </summary>
    public class HorseClient : ClientSocketBase<HorseMessage>, IDisposable
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
        private readonly MessageTracker _tracker;

        /// <summary>
        /// HMQ Client Direct message management object
        /// </summary>
        public DirectOperator Direct { get; }

        /// <summary>
        /// HMQ Client Queue Management object
        /// </summary>
        public QueueOperator Queues { get; }

        /// <summary>
        /// HMQ Client Connection Management object
        /// </summary>
        public ConnectionOperator Connections { get; }

        /// <summary>
        /// HMQ Client Router Management object
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
        /// Creates new HMQ protocol client
        /// </summary>
        public HorseClient()
        {
            Data.Method = "CONNECT";
            Data.Path = "/";

            Direct = new DirectOperator(this);
            Queues = new QueueOperator(this);
            Connections = new ConnectionOperator(this);
            Routers = new RouterOperator(this);

            Events = new EventManager();
            DeliveryContainer = new TypeDeliveryContainer(new TypeDeliveryResolver());

            _tracker = new MessageTracker(this);
            _tracker.Run();
        }

        /// <summary>
        /// Releases all resources of the client
        /// </summary>
        public void Dispose()
        {
            _tracker?.Dispose();
            Queues.Dispose();
        }

        #endregion

        #region Connection Data

        /// <summary>
        /// Sets client type information of the client
        /// </summary>
        public void SetClientType(string type)
        {
            if (Data.Properties.ContainsKey(HorseHeaders.CLIENT_TYPE))
                Data.Properties[HorseHeaders.CLIENT_TYPE] = type;
            else
                Data.Properties.Add(HorseHeaders.CLIENT_TYPE, type);
        }

        /// <summary>
        /// Sets client name information of the client
        /// </summary>
        public void SetClientName(string name)
        {
            if (Data.Properties.ContainsKey(HorseHeaders.CLIENT_NAME))
                Data.Properties[HorseHeaders.CLIENT_NAME] = name;
            else
                Data.Properties.Add(HorseHeaders.CLIENT_NAME, name);
        }

        /// <summary>
        /// Sets client token information of the client
        /// </summary>
        public void SetClientToken(string token)
        {
            if (Data.Properties.ContainsKey(HorseHeaders.CLIENT_TOKEN))
                Data.Properties[HorseHeaders.CLIENT_TOKEN] = token;
            else
                Data.Properties.Add(HorseHeaders.CLIENT_TOKEN, token);
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

            if (host.Protocol != Protocol.Hmq)
                throw new NotSupportedException("Only HMQ protocol is supported");

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

            if (host.Protocol != Protocol.Hmq)
                throw new NotSupportedException("Only HMQ protocol is supported");

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
                    throw new NotSupportedException("Unsupported HMQ Protocol version. Server supports: " + Encoding.UTF8.GetString(buffer));
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

            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Hello;

            message.Content = new MemoryStream();

            Data.Path = string.IsNullOrEmpty(dns.Path) ? "/" : dns.Path;
            if (string.IsNullOrEmpty(Data.Method))
                Data.Method = "NONE";

            string first = Data.Method + " " + Data.Path + "\r\n";
            await message.Content.WriteAsync(Encoding.UTF8.GetBytes(first));

            if (Data.Properties.ContainsKey(HorseHeaders.CLIENT_ID))
                Data.Properties[HorseHeaders.CLIENT_ID] = ClientId;
            else
                Data.Properties.Add(HorseHeaders.CLIENT_ID, ClientId);

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
            HmqReader reader = new HmqReader();
            HorseMessage message = await reader.Read(Stream);

            if (message == null)
            {
                Disconnect();
                return;
            }

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

                case MessageType.Response:
                    _tracker.Process(message);

                    if (CatchResponseMessages)
                        SetOnMessageReceived(message);
                    break;

                case MessageType.Event:
                    _ = Events.TriggerEvents(this, message);
                    break;

                case MessageType.QueueMessage:

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    //if message is response for pull request, process pull container
                    if (Queues.PullContainers.Count > 0 && message.HasHeader)
                    {
                        string requestId = message.FindHeader(HorseHeaders.REQUEST_ID);
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

                    if (message.WaitResponse && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    SetOnMessageReceived(message);
                    break;
            }
        }

        /// <summary>
        /// Processes pull message
        /// </summary>
        private void ProcessPull(string requestId, HorseMessage message, PullContainer container)
        {
            if (message.Length > 0)
                container.AddMessage(message);

            string noContent = message.FindHeader(HorseHeaders.NO_CONTENT);

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
            _tracker.MarkAllMessagesExpired();
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
        public override void Pong(object pingMessage = null)
        {
            Send(PredefinedMessages.PONG);
        }

        #endregion

        #region Send

        /// <summary>
        /// Sends a HMQ message
        /// </summary>
        public bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = HmqWriter.Create(message, additionalHeaders);
            return Send(data);
        }

        /// <summary>
        /// Sends a HMQ message
        /// </summary>
        public async Task<HorseResult> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = HmqWriter.Create(message, additionalHeaders);
            bool sent = await SendAsync(data);
            return sent ? HorseResult.Ok() : new HorseResult(HorseResultCode.SendError);
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

            Task<HorseMessage> task = _tracker.Track(message);
            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
                return new HorseModelResult<T>(new HorseResult(HorseResultCode.SendError));

            HorseMessage response = await task;
            if (response?.Content == null || response.Length == 0 || response.Content.Length == 0)
                return HorseModelResult<T>.FromContentType(message.ContentType);

            T model = response.Deserialize<T>(JsonSerializer);
            return new HorseModelResult<T>(HorseResult.Ok(), model);
        }

        /// <summary>
        /// Sends a response message to the request
        /// </summary>
        public async Task<HorseResult> SendResponseAsync<TModel>(HorseMessage requestMessage, TModel responseModel)
        {
            HorseMessage response = requestMessage.CreateResponse(HorseResultCode.Ok);
            response.Serialize(responseModel, JsonSerializer);
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

            Task<HorseMessage> task = _tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                _tracker.Forget(message);
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
                task = _tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                if (waitForResponse)
                    _tracker.Forget(message);

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

            Task<HorseMessage> task = _tracker.Track(message);

            HorseResult sent = await SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                _tracker.Forget(message);
                return false;
            }

            HorseMessage response = await task;
            if (response == null)
                return false;

            HorseResultCode code = (HorseResultCode) response.ContentType;
            return code == HorseResultCode.Ok;
        }

        #endregion
    }
}