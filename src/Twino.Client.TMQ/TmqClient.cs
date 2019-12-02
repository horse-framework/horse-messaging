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
        /// If true, response messages will trigger on message received events.
        /// If false, response messages are proceed silently.
        /// </summary>
        public bool CatchResponseMessages { get; set; }

        /// <summary>
        /// Maximum time to wait acknowledge message
        /// </summary>
        public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum time to wait response message
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(15);

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

        #region Connect - Read

        /// <summary>
        /// Connects to well defined remote host
        /// </summary>
        public override void Connect(DnsInfo host)
        {
            if (string.IsNullOrEmpty(_clientId))
                _clientId = UniqueIdGenerator.Create();

            try
            {
                Client = new TcpClient();
                Client.Connect(host.IPAddress, host.Port);
                IsConnected = true;

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

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES.Length];
                int len = Stream.Read(buffer, 0, buffer.Length);

                CheckProtocolResponse(buffer, len);

                SendInfoMessage().Wait();
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
            try
            {
                Client = new TcpClient();
                await Client.ConnectAsync(host.IPAddress, host.Port);
                IsConnected = true;

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

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES.Length];
                int len = await Stream.ReadAsync(buffer, 0, buffer.Length);

                CheckProtocolResponse(buffer, len);

                await SendInfoMessage();
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
        private async Task SendInfoMessage()
        {
            if (Data?.Properties == null || Data.Properties.Count < 1 && string.IsNullOrEmpty(Data.Method) && string.IsNullOrEmpty(Data.Path))
                return;

            TmqMessage message = new TmqMessage();
            message.FirstAcquirer = true;
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Hello;

            message.Content = new MemoryStream();

            string path = "/";
            if (!string.IsNullOrEmpty(Data.Path))
                path = Data.Path.Replace(" ", "+");

            string first = (Data.Method ?? "NONE") + " " + path + "\r\n";
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

            switch (message.Type)
            {
                case MessageType.Terminate:
                    Disconnect();
                    break;

                case MessageType.Ping:
                    Pong();
                    break;

                case MessageType.Acknowledge:
                    _follower.ProcessAcknowledge(message);
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
            message.Source = _clientId;

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.MessageId = UniqueIdGenerator.Create();

            message.CalculateLengths();

            byte[] data = _writer.Create(message).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends a TMQ message
        /// </summary>
        public async Task<bool> SendAsync(TmqMessage message)
        {
            message.Source = _clientId;

            if (string.IsNullOrEmpty(message.MessageId) && UseUniqueMessageId)
                message.MessageId = UniqueIdGenerator.Create();

            message.CalculateLengths();

            byte[] data = await _writer.Create(message);
            return await SendAsync(data);
        }

        #endregion

        #region MQ Operations

        /// <summary>
        /// Joins to a channel
        /// </summary>
        public async Task<bool> Join(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Join;
            message.Target = channel;
            message.ResponseRequired = verifyResponse;
            message.MessageId = UniqueIdGenerator.Create();

            bool sent = await SendAsync(message);
            if (!sent)
                return false;

            if (verifyResponse)
            {
                TmqMessage response = await _follower.FollowResponse(message);
                return response != null && response.ContentType == KnownContentTypes.Ok;
            }

            return true;
        }

        /// <summary>
        /// Leaves from a channel
        /// </summary>
        public async Task<bool> Leave(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Leave;
            message.Target = channel;
            message.ResponseRequired = verifyResponse;
            message.MessageId = UniqueIdGenerator.Create();

            bool sent = await SendAsync(message);
            if (!sent)
                return false;

            if (verifyResponse)
            {
                TmqMessage response = await _follower.FollowResponse(message);
                return response != null && response.ContentType == KnownContentTypes.Ok;
            }

            return true;
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<bool> Push(string channel, ushort contentType, MemoryStream content, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Channel;
            message.ContentType = contentType;
            message.Target = channel;
            message.MessageId = UniqueIdGenerator.Create();
            message.Content = content;

            bool sent = await SendAsync(message);
            if (!sent)
                return false;

            if (waitAcknowledge)
                return await _follower.FollowAcknowledge(message);

            return true;
        }

        #endregion
    }
}