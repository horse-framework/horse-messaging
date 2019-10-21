using Twino.Server;
using Twino.Server.WebSockets;
using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Sample.Full.Server
{
    public class ClientFactory : IClientFactory
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            return await Task.FromResult(new Client(server, request, client));
        }
    }
}
