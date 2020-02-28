using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// TMQ Client class
    /// Can be used directly with event subscriptions
    /// Or can be base class to a derived Client class and provides virtual methods for all events
    /// </summary>
    public class TmqClient : ClientSocketBase<TmqMessage>, IDisposable
    {
        #region Properties

        /// <summary>
        /// TMQ Procotol message writer
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

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

        #endregion

        #region Constructors - Destructors

        /// <summary>
        /// Creates new TMQ protocol client
        /// </summary>
        public TmqClient()
        {
            Data.Method = "CONNECT";
            Data.Path = "/";
            _follower = new MessageFollower(this);
            _follower.Run();
        }

        /// <summary>
        /// Releases all resources of the client
        /// </summary>
        public void Dispose()
        {
            _follower?.Dispose();
        }

        #endregion

        #region Connection Data

        /// <summary>
        /// Sets client type information of the client
        /// </summary>
        public void SetClientType(string type)
        {
            if (Data.Properties.ContainsKey(TmqHeaders.CLIENT_TYPE))
                Data.Properties[TmqHeaders.CLIENT_TYPE] = type;
            else
                Data.Properties.Add(TmqHeaders.CLIENT_TYPE, type);
        }

        /// <summary>
        /// Sets client name information of the client
        /// </summary>
        public void SetClientName(string name)
        {
            if (Data.Properties.ContainsKey(TmqHeaders.CLIENT_NAME))
                Data.Properties[TmqHeaders.CLIENT_NAME] = name;
            else
                Data.Properties.Add(TmqHeaders.CLIENT_NAME, name);
        }

        /// <summary>
        /// Sets client token information of the client
        /// </summary>
        public void SetClientToken(string token)
        {
            if (Data.Properties.ContainsKey(TmqHeaders.CLIENT_TOKEN))
                Data.Properties[TmqHeaders.CLIENT_TOKEN] = token;
            else
                Data.Properties.Add(TmqHeaders.CLIENT_TOKEN, token);
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

                Stream.Write(PredefinedMessages.PROTOCOL_BYTES);
                SendInfoMessage(host).Wait();

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES.Length];
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

                await Stream.WriteAsync(PredefinedMessages.PROTOCOL_BYTES);
                await SendInfoMessage(host);

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES.Length];
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
        private static void CheckProtocolResponse(byte[] buffer, int len)
        {
            if (len < PredefinedMessages.PROTOCOL_BYTES.Length)
                throw new InvalidOperationException("Unexpected server response");

            for (int i = 0; i < PredefinedMessages.PROTOCOL_BYTES.Length; i++)
                if (PredefinedMessages.PROTOCOL_BYTES[i] != buffer[i])
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
            if (Data?.Properties == null || Data.Properties.Count < 1 && string.IsNullOrEmpty(Data.Method) && string.IsNullOrEmpty(Data.Path))
                return;

            TmqMessage message = new TmqMessage();
            message.FirstAcquirer = true;
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Hello;

            message.Content = new MemoryStream();

            Data.Path = string.IsNullOrEmpty(dns.Path) ? "/" : dns.Path;
            if (string.IsNullOrEmpty(Data.Method))
                Data.Method = "NONE";

            string first = Data.Method + " " + Data.Path + "\r\n";
            await message.Content.WriteAsync(Encoding.UTF8.GetBytes(first));
            Data.Properties.Add(TmqHeaders.CLIENT_ID, ClientId);

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
            TmqMessage message = await reader.Read(Stream);

            if (message == null)
            {
                Disconnect();
                return;
            }

            if (message.Ttl < 0)
                return;

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

                default:
                    if (message.AcknowledgeRequired && AutoAcknowledge)
                        await SendAsync(message.CreateAcknowledge());

                    SetOnMessageReceived(message);
                    break;
            }
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
        public bool Send(TmqMessage message)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = _writer.Create(message).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends a json object message
        /// </summary>
        public bool SendJson(string target, ushort contentType, object model)
        {
            TmqMessage msg = new TmqMessage();
            msg.SetTarget(target);
            msg.ContentType = contentType;
            msg.SetJsonContent(model).Wait();

            return Send(msg);
        }

        /// <summary>
        /// Sends a string message
        /// </summary>
        public bool Send(string target, ushort contentType, string message)
        {
            TmqMessage msg = new TmqMessage();
            msg.SetTarget(target);
            msg.ContentType = contentType;
            msg.SetStringContent(message);

            return Send(msg);
        }

        /// <summary>
        /// Sends a memory stream message
        /// </summary>
        public bool Send(string target, ushort contentType, MemoryStream content)
        {
            TmqMessage message = new TmqMessage();
            message.SetTarget(target);
            message.ContentType = contentType;
            message.Content = content;

            return Send(message);
        }

        /// <summary>
        /// Sends a TMQ message
        /// </summary>
        public async Task<bool> SendAsync(TmqMessage message)
        {
            message.SetSource(_clientId);

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            byte[] data = await _writer.Create(message);
            return await SendAsync(data);
        }

        /// <summary>
        /// Sends a TMQ message and waits for acknowledge
        /// </summary>
        public async Task<bool> SendWithAcknowledge(TmqMessage message)
        {
            message.SetSource(_clientId);
            message.AcknowledgeRequired = true;
            message.ResponseRequired = false;
            message.SetMessageId(UniqueIdGenerator.Create());

            if (string.IsNullOrEmpty(message.MessageId))
                throw new ArgumentNullException("Messages without unique id cannot be acknowledged");

            return await WaitForAcknowledge(message, true);
        }

        /// <summary>
        /// Sends a json object message
        /// </summary>
        public async Task<bool> SendJsonAsync(string target, ushort contentType, object model, bool waitAcknowledge)
        {
            TmqMessage msg = new TmqMessage();
            msg.SetTarget(target);
            msg.ContentType = contentType;
            await msg.SetJsonContent(model);

            if (waitAcknowledge)
                return await SendWithAcknowledge(msg);

            return await SendAsync(msg);
        }

        /// <summary>
        /// Sends a string message
        /// </summary>
        public async Task<bool> SendAsync(string target, ushort contentType, string message, bool waitAcknowledge)
        {
            TmqMessage msg = new TmqMessage();
            msg.SetTarget(target);
            msg.ContentType = contentType;
            msg.SetStringContent(message);

            if (waitAcknowledge)
                return await SendWithAcknowledge(msg);

            return await SendAsync(msg);
        }

        /// <summary>
        /// Sends a memory stream message
        /// </summary>
        public async Task<bool> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.SetTarget(target);
            message.ContentType = contentType;
            message.Content = content;

            if (waitAcknowledge)
                return await SendWithAcknowledge(message);

            return await SendAsync(message);
        }

        /// <summary>
        /// Sends a message, waits response and deserializes JSON response to T template type
        /// </summary>
        public async Task<TmqResult<T>> SendAndGetJson<T>(TmqMessage message)
        {
            message.ResponseRequired = true;

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(UniqueIdGenerator.Create());

            Task<TmqMessage> task = _follower.FollowResponse(message);
            bool sent = await SendAsync(message);
            if (!sent)
                return new TmqResult<T>(TmqResponseCode.SendError);

            TmqMessage response = await task;
            if (response?.Content == null || response.Length == 0 || response.Content.Length == 0)
                return TmqResult<T>.FromContentType(message.ContentType);

            T model = await response.GetJsonContent<T>();
            return new TmqResult<T>(TmqResponseCode.Ok, model);
        }

        #endregion

        #region Send By

        /// <summary>
        /// Sends a JSON message by receiver name
        /// </summary>
        public async Task<bool> SendJsonByNameAsync<T>(string name, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            return await SendJsonToFullAsync("@name:" + name, contentType, model, toOnlyFirstReceiver, waitAcknowledge);
        }

        /// <summary>
        /// Sends a JSON message by receiver type
        /// </summary>
        public async Task<bool> SendJsonByTypeAsync<T>(string type, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            return await SendJsonToFullAsync("@type:" + type, contentType, model, toOnlyFirstReceiver, waitAcknowledge);
        }

        /// <summary>
        /// Sends a JSON message by full name
        /// </summary>
        private async Task<bool> SendJsonToFullAsync<T>(string fullname, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.SetTarget(fullname);
            message.FirstAcquirer = toOnlyFirstReceiver;
            message.ContentType = contentType;
            await message.SetJsonContent(model);

            if (waitAcknowledge)
                return await SendWithAcknowledge(message);

            return await SendAsync(message);
        }

        /// <summary>
        /// Sends a memory stream message by receiver name
        /// </summary>
        public async Task<bool> SendByNameAsync(string name, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.SetTarget("@name:" + name);
            message.FirstAcquirer = toOnlyFirstReceiver;
            message.ContentType = contentType;
            message.Content = content;

            if (waitAcknowledge)
                return await SendWithAcknowledge(message);

            return await SendAsync(message);
        }

        /// <summary>
        /// Sends a memory stream message by receiver type
        /// </summary>
        public async Task<bool> SendByTypeAsync(string type, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            return await SendByFullAsync("@type:" + type, contentType, content, toOnlyFirstReceiver, waitAcknowledge);
        }

        /// <summary>
        /// Sends a memory stream message by full name
        /// </summary>
        private async Task<bool> SendByFullAsync(string fullname, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.SetTarget(fullname);
            message.FirstAcquirer = toOnlyFirstReceiver;
            message.ContentType = contentType;
            message.Content = content;

            if (waitAcknowledge)
                return await SendWithAcknowledge(message);

            return await SendAsync(message);
        }

        #endregion

        #region Acknowledge - Response

        /// <summary>
        /// Sends acknowledge message for the message
        /// </summary>
        public async Task<bool> Acknowledge(TmqMessage message)
        {
            if (!message.AcknowledgeRequired)
                return false;

            TmqMessage ack = message.CreateAcknowledge();
            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends unacknowledge message for the message.
        /// </summary>
        public async Task<bool> SendUnacknowledge(TmqMessage message)
        {
            if (!message.AcknowledgeRequired)
                return false;

            TmqMessage ack = message.CreateAcknowledge();
            ack.SetStringContent("FAILED");

            return await SendAsync(ack);
        }

        /// <summary>
        /// Sends message.
        /// if verify requires, waits response and checkes status code of the response.
        /// returns true if Ok.
        /// </summary>
        protected async Task<TmqResponseCode> WaitResponse(TmqMessage message, bool verifyResponse)
        {
            Task<TmqMessage> task = null;
            if (verifyResponse)
                task = _follower.FollowResponse(message);

            bool sent = await SendAsync(message);
            if (!sent)
                return TmqResponseCode.SendError;

            if (verifyResponse)
            {
                TmqMessage response = await task;
                return response == null
                           ? TmqResponseCode.Unknown
                           : (TmqResponseCode)response.ContentType;
            }

            return TmqResponseCode.Ok;
        }

        /// <summary>
        /// Sends message.
        /// if acknowledge is pending, waits for acknowledge.
        /// returns true if received.
        /// </summary>
        private async Task<bool> WaitForAcknowledge(TmqMessage message, bool waitAcknowledge)
        {
            Task<bool> task = null;
            if (waitAcknowledge)
                task = _follower.FollowAcknowledge(message);

            bool sent = await SendAsync(message);
            if (!waitAcknowledge || !sent)
                return sent;

            return await task;
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a request to target with a JSON model, waits response
        /// </summary>
        public async Task<TmqMessage> RequestJson(string target, ushort contentType, object model)
        {
            TmqMessage message = new TmqMessage(MessageType.Client);
            message.SetTarget(target);
            message.ContentType = contentType;
            await message.SetJsonContent(model);

            return await Request(message);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<TmqMessage> Request(string target, ushort contentType, MemoryStream content)
        {
            TmqMessage message = new TmqMessage(MessageType.Client);
            message.SetTarget(target);
            message.Content = content;
            message.ContentType = contentType;

            return await Request(message);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<TmqMessage> Request(string target, ushort contentType, string content)
        {
            TmqMessage message = new TmqMessage(MessageType.Client);
            message.SetTarget(target);
            message.ContentType = contentType;
            message.SetStringContent(content);

            return await Request(message);
        }

        /// <summary>
        /// Sends the message and waits for response
        /// </summary>
        public async Task<TmqMessage> Request(TmqMessage message)
        {
            message.ResponseRequired = true;
            message.AcknowledgeRequired = false;
            message.SetMessageId(UniqueIdGenerator.Create());

            bool sent = await SendAsync(message);
            if (!sent)
                return null;

            return await _follower.FollowResponse(message);
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<bool> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ContentType = queueId;
            message.SetTarget(channel);
            message.Content = new MemoryStream();
            message.AcknowledgeRequired = waitAcknowledge;
            await System.Text.Json.JsonSerializer.SerializeAsync(message.Content, jsonObject, jsonObject.GetType());

            if (waitAcknowledge)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitForAcknowledge(message, waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<bool> Push(string channel, ushort queueId, string content, bool waitAcknowledge)
        {
            return await Push(channel, queueId, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<bool> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ContentType = queueId;
            message.SetTarget(channel);
            message.Content = content;
            message.AcknowledgeRequired = waitAcknowledge;

            if (waitAcknowledge)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitForAcknowledge(message, waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue and does not wait for acknowledge.
        /// Uses legacy callback method instead of async
        /// </summary>
        public bool PushJsonSync(string channel, ushort queueId, object jsonObject)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ContentType = queueId;
            message.SetTarget(channel);
            message.AcknowledgeRequired = false;
            byte[] data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(jsonObject, jsonObject.GetType());
            message.Content = new MemoryStream(data);
            message.Content.Position = 0;

            if (UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            return Send(message);
        }

        /// <summary>
        /// Pushes a message to a queue and does not wait for acknowledge.
        /// Uses legacy callback method instead of async
        /// </summary>
        public bool PushSync(string channel, ushort queueId, byte[] data)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ContentType = queueId;
            message.SetTarget(channel);
            message.AcknowledgeRequired = false;
            message.Content = new MemoryStream(data);
            message.Content.Position = 0;

            if (UseUniqueMessageId)
                message.SetMessageId(UniqueIdGenerator.Create());

            return Send(message);
        }

        #endregion

        #region Pull

        /// <summary>
        /// Request a message from Pull queue
        /// </summary>
        public async Task<TmqMessage> Pull(string channel, ushort queueId)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ResponseRequired = true;
            message.ContentType = queueId;
            message.SetTarget(channel);
            message.SetMessageId(UniqueIdGenerator.Create());

            Task<TmqMessage> task = _follower.FollowResponse(message);
            bool sent = await SendAsync(message);
            if (!sent)
                return null;

            TmqMessage response = await task;
            if (response.Content == null || response.Length == 0 || response.Content.Length == 0)
                return null;

            return response;
        }

        /// <summary>
        /// Request a message from Pull queue
        /// </summary>
        public async Task<TModel> PullJson<TModel>(string channel, ushort queueId)
        {
            TmqMessage response = await Pull(channel, queueId);
            if (response?.Content == null || response.Length == 0 || response.Content.Length == 0)
                return default;

            return await response.GetJsonContent<TModel>();
        }

        #endregion

        #region Channel

        /// <summary>
        /// Joins to a channel
        /// </summary>
        public async Task<TmqResponseCode> Join(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Join;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Leaves from a channel
        /// </summary>
        public async Task<TmqResponseCode> Leave(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Leave;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Creates a new channel without any queue
        /// </summary>
        public async Task<TmqResponseCode> CreateChannel(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateChannel;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Creates a new channel without any queue
        /// </summary>
        public async Task<TmqResponseCode> CreateChannel(string channel, Action<ChannelCreationOptions> optionsAction)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateChannel;
            message.SetTarget(channel);
            message.ResponseRequired = true;
            message.SetMessageId(UniqueIdGenerator.Create());

            ChannelCreationOptions options = new ChannelCreationOptions();
            optionsAction(options);
            message.Content = new MemoryStream(Encoding.UTF8.GetBytes(options.Serialize(0)));

            return await WaitResponse(message, true);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public async Task<TmqResponseCode> CreateQueue(string channel, ushort queueId, bool verifyResponse, Action<QueueOptions> optionsAction = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateQueue;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;

            if (optionsAction == null)
                message.Content = new MemoryStream(BitConverter.GetBytes(queueId));
            else
            {
                QueueOptions options = new QueueOptions();
                optionsAction(options);
                message.Content = new MemoryStream(Encoding.UTF8.GetBytes(options.Serialize(queueId)));
            }

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        #endregion
    }
}