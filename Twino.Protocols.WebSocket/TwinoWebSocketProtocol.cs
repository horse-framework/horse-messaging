using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public class TwinoWebSocketProtocol : ITwinoProtocol<WebSocketMessage>
    {
        public string Name => "websocket";

        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;

        public async Task<ProtocolHandshakeResult> Check(byte[] data)
        {
            return await Task.FromResult(new ProtocolHandshakeResult());
        }

        public IProtocolMessageReader<WebSocketMessage> CreateReader()
        {
            return new WebSocketReader();
        }

        public IProtocolMessageWriter<WebSocketMessage> CreateWriter()
        {
            return new WebSocketWriter();
        }
    }
}