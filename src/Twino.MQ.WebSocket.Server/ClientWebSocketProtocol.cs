using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.WebSocket.Server
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

        public bool Send(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> SendAsync(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            throw new System.NotImplementedException();
        }
    }
}