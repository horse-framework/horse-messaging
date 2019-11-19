using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.Http;

namespace Twino.Protocols.WebSocket
{
    internal class WebSocketHttpHandler : IProtocolConnectionHandler<HttpMessage>
    {
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, Dictionary<string, string> properties)
        {
            return await Task.FromResult((SocketBase) null);
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, HttpMessage message)
        {
            message.Response.StatusCode = HttpStatusCode.NotFound;
            await Task.CompletedTask;
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}