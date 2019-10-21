using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.WebSockets
{

    /// <summary>
    /// Client factory delegate for web socket
    /// </summary>
    public delegate Task<ServerSocket> ClientFactoryHandler(TwinoServer server, HttpRequest request, TcpClient client);

    /// <summary>
    /// Created for client factory implementation of web socket server.
    /// When developer does not want to create new factory class and want to create client via methods.
    /// This factory will be created and points the method delegate
    /// </summary>
    public class DefaultClientFactory : IClientFactory
    {

        private readonly ClientFactoryHandler _handler;

        public DefaultClientFactory(ClientFactoryHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Creates new Server-Side WebSocket client
        /// </summary>
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            return await _handler(server, request, client);
        }
    }
}
