using Horse.Core;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;
using Horse.Protocols.WebSocket;

namespace Horse.Mq.WebSocket.Server
{
	public class HmqWebSocketClient : MqClient
	{
		internal WsServerSocket Socket { get; }

		public HmqWebSocketClient(WsServerSocket socket, HorseMq server, IConnectionInfo info) : base(server, info)
		{
			Socket = socket;
		}

		public HmqWebSocketClient(WsServerSocket socket, HorseMq server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
			: base(server, info, generator, useUniqueMessageId)
		{
			Socket = socket;
		}
	}
}