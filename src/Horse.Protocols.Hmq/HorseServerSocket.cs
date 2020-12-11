using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Core;

namespace Horse.Protocols.Hmq
{
    /// <summary>
    /// HMQ Protocol socket object for HMQ servers
    /// </summary>
    public class HorseServerSocket : SocketBase
    {
        /// <summary>
        /// Server of the socket
        /// </summary>
        public IHorseServer Server { get; }

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

        private Action<HorseServerSocket> _cleanupAction;

        /// <summary>
        /// Creates new HMQ Server-side socket client
        /// </summary>
        public HorseServerSocket(IHorseServer server, IConnectionInfo info)
            : this(server, info, new DefaultUniqueIdGenerator())
        {
        }

        /// <summary>
        /// Creates new HMQ Server-side socket client
        /// </summary>
        public HorseServerSocket(IHorseServer server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(info)
        {
            Client = info.Client;
            Server = server;
            Info = info;
            _uniqueIdGenerator = generator;
            UseUniqueMessageId = useUniqueMessageId;
        }

        /// <summary>
        /// Completed disconnect operations in hmq server-side client
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
        internal void SetCleanupAction(Action<HorseServerSocket> action)
        {
            _cleanupAction = action;
        }

        /// <summary>
        /// Sends HMQ ping message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends HMQ pong message
        /// </summary>
        public override void Pong(object pingMessage = null)
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends HMQ message to client
        /// </summary>
        public virtual bool Send(HorseMessage message, IList<KeyValuePair<string,string>> additionalHeaders = null)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = HmqWriter.Create(message, additionalHeaders);
            return Send(data);
        }

        /// <summary>
        /// Sends HMQ message to client
        /// </summary>
        public virtual Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string,string>> additionalHeaders = null)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = HmqWriter.Create(message, additionalHeaders);
            return SendAsync(data);
        }
    }
}