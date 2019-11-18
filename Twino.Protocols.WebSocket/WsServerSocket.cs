using Twino.Core;

namespace Twino.Protocols.WebSocket
{
    public class WsServerSocket : SocketBase
    {
        public ITwinoServer Server { get; }
        public IConnectionInfo Info { get; }

        public WsServerSocket(ITwinoServer server, IConnectionInfo info)
        {
            Server = server;
            Info = info;
        }

        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }
    }
}