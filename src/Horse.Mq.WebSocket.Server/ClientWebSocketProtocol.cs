using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.WebSocket.Server
{
    public class ClientWebSocketProtocol : IClientCustomProtocol
    {
        public void Ping()
        {
            throw new System.NotImplementedException();
        }

        public void Pong(object pingMessage = null)
        {
            throw new System.NotImplementedException();
        }

        public bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            throw new System.NotImplementedException();
        }
    }
}