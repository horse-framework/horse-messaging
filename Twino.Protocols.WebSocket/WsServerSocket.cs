using Twino.Core;

namespace Twino.Protocols.WebSocket
{
    public class WsServerSocket : SocketBase
    {
        private static readonly WebSocketWriter _writer = new WebSocketWriter();

        public ITwinoServer Server { get; }
        public IConnectionInfo Info { get; }

        public WsServerSocket(ITwinoServer server, IConnectionInfo info)
        {
            Server = server;
            Info = info;
            Stream = info.GetStream();
        }

        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        public void Send(string message)
        {
            byte[] data = _writer.Create(WebSocketMessage.FromString(message)).Result;
            Send(data);
        }
    }
}