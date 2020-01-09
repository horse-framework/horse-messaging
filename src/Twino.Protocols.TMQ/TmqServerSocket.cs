using System;
using System.Threading.Tasks;
using Twino.Core;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Protocol socket object for TMQ servers
    /// </summary>
    public class TmqServerSocket : SocketBase
    {
        /// <summary>
        /// WebSocketWriter singleton instance
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        /// <summary>
        /// Server of the socket
        /// </summary>
        public ITwinoServer Server { get; }

        /// <summary>
        /// Socket's connection information
        /// </summary>
        public IConnectionInfo Info { get; }

        /// <summary>
        /// If true, each message has it's own unique message id.
        /// IF false, message unique id value will be sent as empty.
        /// Default value is true
        /// </summary>
        public bool UseUniqueMessageId { get; set; }

        private readonly IUniqueIdGenerator _uniqueIdGenerator;

        private Action<TmqServerSocket> _cleanupAction;

        /// <summary>
        /// Creates new TMQ Server-side socket client
        /// </summary>
        public TmqServerSocket(ITwinoServer server, IConnectionInfo info)
            : this(server, info, new DefaultUniqueIdGenerator())
        {
        }

        /// <summary>
        /// Creates new TMQ Server-side socket client
        /// </summary>
        public TmqServerSocket(ITwinoServer server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(info)
        {
            Client = info.Client;
            Server = server;
            Info = info;
            _uniqueIdGenerator = generator;
            UseUniqueMessageId = useUniqueMessageId;
        }

        /// <summary>
        /// Completed disconnect operations in tmq server-side client
        /// </summary>
        protected override void OnDisconnected()
        {
            if (_cleanupAction != null)
                _cleanupAction(this);

            base.OnDisconnected();
        }

        /// <summary>
        /// Runs cleanup action
        /// </summary>
        internal void SetCleanupAction(Action<TmqServerSocket> action)
        {
            _cleanupAction = action;
        }

        /// <summary>
        /// Sends TMQ ping message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends TMQ pong message
        /// </summary>
        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public bool Send(TmqMessage message)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = _writer.Create(message).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public async Task<bool> SendAsync(TmqMessage message)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = await _writer.Create(message);
            return await SendAsync(data);
        }
    }
}