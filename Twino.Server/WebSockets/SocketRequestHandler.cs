using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Http;
using HttpHeaders = Twino.Core.Http.HttpHeaders;

namespace Twino.Server.WebSockets
{
    /// <summary>
    /// Handles WebSocket HTTP Requests.
    /// Checks the WebSocketKey with SHA1 algoritm and creates response key hash.
    /// Creates new 101 Switching Protocol response.
    /// </summary>
    internal class SocketRequestHandler
    {
        /// <summary>
        /// Requested server object
        /// </summary>
        internal TwinoServer Server { get; set; }

        /// <summary>
        /// HTTP Request belong the WebSocket reques operation
        /// </summary>
        internal HttpRequest Request { get; set; }

        /// <summary>
        /// Connected client's TCP Socket class
        /// </summary>
        internal TcpClient Client { get; set; }

        public SocketRequestHandler(TwinoServer server, HttpRequest request, TcpClient tcp)
        {
            Server = server;
            Request = request;
            Client = tcp;
        }

        /// <summary>
        /// Completes WebSocket Protocol Handshaking operations
        /// </summary>
        internal async Task HandshakeClient()
        {
            if (!Request.IsWebSocket)
            {
                Client.Close();
                Client.Dispose();
                return;
            }

            byte[] response = BuildResponse(Request.WebSocketKey);
            await Request.Response.NetworkStream.WriteAsync(response, 0, response.Length);

            await Task.Yield();
            ServerSocket client = await Server.ClientFactory.Create(Server, Request, Client);
            Server.SetClientConnected(client);
            client.Start();
            Server.Pinger.AddClient(client);
        }

        /// <summary>
        /// Creates WebSocket Switching Protocol Response
        /// </summary>
        private byte[] BuildResponse(string websocketKey)
        {
            byte[] response = Encoding.UTF8.GetBytes(HttpHeaders.HTTP_VERSION + " 101 Switching Protocols" + Environment.NewLine +
                                                     HttpHeaders.Create(HttpHeaders.SERVER, HttpHeaders.VALUE_SERVER) +
                                                     HttpHeaders.Create(HttpHeaders.CONNECTION, HttpHeaders.UPGRADE) +
                                                     HttpHeaders.Create(HttpHeaders.UPGRADE, HttpHeaders.VALUE_WEBSOCKET) +
                                                     HttpHeaders.Create(HttpHeaders.WEBSOCKET_ACCEPT, CreateWebSocketGuid(websocketKey)) +
                                                     Environment.NewLine);

            return response;
        }

        /// <summary>
        /// Computes response hash from the requested web socket key
        /// </summary>
        private static string CreateWebSocketGuid(string key)
        {
            byte[] keybytes = Encoding.UTF8.GetBytes(key + HttpHeaders.WEBSOCKET_GUID);
            return Convert.ToBase64String(SHA1.Create().ComputeHash(keybytes));
        }
    }
}