using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public class TwinoWebSocketProtocol : ITwinoProtocol<WebSocketMessage>
    {
        public string Name => "websocket";

        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;

        public async Task<bool> Check(byte[] data)
        {
            return await Task.FromResult(false);
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