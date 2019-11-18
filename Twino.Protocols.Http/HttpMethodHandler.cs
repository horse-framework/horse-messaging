using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.Http
{
    public delegate Task HttpRequestHandler(HttpRequest request, HttpResponse response);

    internal class HttpMethodHandler : IProtocolConnectionHandler<HttpMessage>
    {
        private readonly HttpRequestHandler _action;

        public HttpMethodHandler(HttpRequestHandler action)
        {
            _action = action;
        }

        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, Dictionary<string, string> properties)
        {
            return await Task.FromResult((SocketBase) null);
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, HttpMessage message)
        {
            await _action(message.Request, message.Response);
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}