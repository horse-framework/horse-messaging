using System;
using System.Threading.Tasks;
using Twino.Core;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// Websocket Server socket object
    /// </summary>
    public class WsServerSocket : SocketBase
    {
        /// <summary>
        /// WebSocketWriter singleton instance
        /// </summary>
        private static readonly WebSocketWriter _writer = new WebSocketWriter();

        /// <summary>
        /// Server of the socket
        /// </summary>
        public ITwinoServer Server { get; }

        /// <summary>
        /// Socket's connection information
        /// </summary>
        public IConnectionInfo Info { get; }

        private Action<WsServerSocket> _cleanupAction;

        public WsServerSocket(ITwinoServer server, IConnectionInfo info)
            : base(info)
        {
            Client = info.Client;
            Server = server;
            Info = info;
        }

        protected override void OnDisconnected()
        {
            if (_cleanupAction != null)
                _cleanupAction(this);

            base.OnDisconnected();
        }

        /// <summary>
        /// Runs cleanup action
        /// </summary>
        internal void SetCleanupAction(Action<WsServerSocket> action)
        {
            _cleanupAction = action;
        }

        /// <summary>
        /// Sends websocket ping message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends websocket pong message
        /// </summary>
        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends websocket message to client
        /// </summary>
        public bool Send(WebSocketMessage message)
        {
            byte[] data = _writer.Create(message).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends websocket message to client
        /// </summary>
        public async Task<bool> SendAsync(WebSocketMessage message)
        {
            byte[] data = await _writer.Create(message);
            return Send(data);
        }

        /// <summary>
        /// Sends string message to client
        /// </summary>
        public bool Send(string message)
        {
            byte[] data = _writer.Create(WebSocketMessage.FromString(message)).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends string message to client
        /// </summary>
        public async Task SendAsync(string message)
        {
            byte[] data = await _writer.Create(WebSocketMessage.FromString(message));
            Send(data);
        }
    }
}