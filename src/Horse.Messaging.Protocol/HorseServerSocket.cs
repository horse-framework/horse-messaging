using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Core;

namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Horse Protocol socket object for Horse servers
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

        private readonly IUniqueIdGenerator _uniqueIdGenerator;

        private Action<HorseServerSocket> _cleanupAction;

        /// <summary>
        /// Creates new Horse Server-side socket client
        /// </summary>
        public HorseServerSocket(IHorseServer server, IConnectionInfo info)
            : this(server, info, new DefaultUniqueIdGenerator())
        {
        }

        /// <summary>
        /// Creates new Horse Server-side socket client
        /// </summary>
        public HorseServerSocket(IHorseServer server, IConnectionInfo info, IUniqueIdGenerator generator)
            : base(info)
        {
            Client = info.Client;
            Server = server;
            Info = info;
            _uniqueIdGenerator = generator;
        }

        /// <summary>
        /// Completed disconnect operations in Horse server-side client
        /// </summary>
        protected override void OnDisconnected()
        {
            IsConnected = false;
            
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
        /// Sends Horse ping message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends Horse pong message
        /// </summary>
        public override void Pong(object pingMessage = null)
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends Horse message to client
        /// </summary>
        public virtual bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);
            return Send(data);
        }

        /// <summary>
        /// Sends Horse message to client
        /// </summary>
        public virtual Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_uniqueIdGenerator.Create());

            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);
            return SendAsync(data);
        }
    }
}