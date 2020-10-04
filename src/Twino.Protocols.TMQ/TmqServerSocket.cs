using System;
using System.Collections;
using System.Collections.Generic;
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
        public override void Pong(object pingMessage = null)
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public virtual bool Send(TwinoMessage message, IList<KeyValuePair<string,string>> additionalHeaders = null)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = TmqWriter.Create(message, additionalHeaders);
            return Send(data);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public virtual Task<bool> SendAsync(TwinoMessage message, IList<KeyValuePair<string,string>> additionalHeaders = null)
        {
            if (UseUniqueMessageId && string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = TmqWriter.Create(message, additionalHeaders);
            return SendAsync(data);
        }
    }
}