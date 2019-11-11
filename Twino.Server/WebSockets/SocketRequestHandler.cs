using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server.Http;
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
        /// Connection info
        /// </summary>
        internal ConnectionInfo Info { get; set; }

        public SocketRequestHandler(TwinoServer server, HttpRequest request, ConnectionInfo info)
        {
            Server = server;
            Request = request;
            Info = info;
        }

        /// <summary>
        /// Completes WebSocket Protocol Handshaking operations
        /// </summary>
        internal async Task HandshakeClient()
        {
            await using MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(PredefinedHeaders.WEBSOCKET_101_SWITCHING_PROTOCOLS_CRLF);
            await ms.WriteAsync(PredefinedHeaders.SERVER_CRLF);
            await ms.WriteAsync(PredefinedHeaders.CONNECTION_UPGRADE_CRLF);
            await ms.WriteAsync(PredefinedHeaders.UPGRADE_WEBSOCKET_CRLF);
            await ms.WriteAsync(PredefinedHeaders.SEC_WEB_SOCKET_COLON);

            ReadOnlyMemory<byte> memory = Encoding.UTF8.GetBytes(CreateWebSocketGuid(Request.WebSocketKey) + "\r\n\r\n");
            await ms.WriteAsync(memory);

            ms.WriteTo(Request.Response.NetworkStream);

            ServerSocket client = await Server.ClientFactory.Create(Server, Request, Info.Client);
            if (client == null)
            {
                Info.Close();
                return;
            }

            Server.SetClientConnected(client);
            client.Start();
            Server.Pinger.AddClient(client);
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