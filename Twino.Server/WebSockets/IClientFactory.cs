using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.WebSockets
{
    /// <summary>
    /// Implementation of client creation of the HttpServer.
    /// ServerSocket is an abstract class and the server side client classes cannot use directly as ServerSocket.
    /// They must derive from the ServerSocket.
    /// And Creating instance of these classes must be defined within this interface implementation
    /// </summary>
    public interface IClientFactory
    {
        /// <summary>
        /// Creates new Server-Side WebSocket client
        /// </summary>
        Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client);
    }
}