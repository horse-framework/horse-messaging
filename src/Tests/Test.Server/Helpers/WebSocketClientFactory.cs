using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server;

namespace Test.Server.Helpers
{
    public class WebSocketClientFactory : IClientFactory 
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            ServerSocket socket = new ServerSocket(server, request, client);
            socket.MessageReceived += (s, m) => { s.Send("Message from server"); };
            return await Task.FromResult(socket);
        }
    }
}