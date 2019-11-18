using Twino.Server;
using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;
using Xunit;

namespace Test.Server
{
    public class SocketServerTest
    {
        private class ClientFactory : IClientFactory
        {
            public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
            {
                return await Task.FromResult(new ServerSocket(server, request, client));
            }
        }

        [Fact]
        public void RunWithDelegate()
        {
            TwinoServer twino = TwinoServer.CreateWebSocket(async (server, request, client) =>
            {
                ServerSocket socket = new ServerSocket(server, request, client);
                return await Task.FromResult(socket);
            });

            twino.Start(93);
        }

        [Fact]
        public void RunWithFactory()
        {
            var factory = new ClientFactory();
            TwinoServer twino = TwinoServer.CreateWebSocket(factory);

            twino.Start(92);
        }

    }
}
