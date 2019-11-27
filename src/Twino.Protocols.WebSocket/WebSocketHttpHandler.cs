using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.Http;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// Default websocekt connection handler
    /// </summary>
    internal class WebSocketHttpHandler : IProtocolConnectionHandler<HttpMessage>
    {
        /// <summary>
        /// Triggered when a websocket client is connected. 
        /// </summary>
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult((SocketBase) null);
        }

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, HttpMessage message)
        {
            message.Response.StatusCode = HttpStatusCode.NotFound;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when a websocket client is disconnected. 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}