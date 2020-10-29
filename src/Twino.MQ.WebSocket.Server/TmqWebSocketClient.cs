using Twino.Core;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;

namespace Twino.MQ.WebSocket.Server
{
    public class TmqWebSocketClient : MqClient
    {
        internal WsServerSocket Socket { get; }

        public TmqWebSocketClient(WsServerSocket socket, TwinoMQ server, IConnectionInfo info) : base(server, info)
        {
            Socket = socket;
        }

        public TmqWebSocketClient(WsServerSocket socket, TwinoMQ server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(server, info, generator, useUniqueMessageId)
        {
            Socket = socket;
        }
    }
}