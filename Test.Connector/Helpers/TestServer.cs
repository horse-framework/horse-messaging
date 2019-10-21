using System.Threading.Tasks;
using Twino.Server;
using Twino.Server.WebSockets;

namespace Test.Connector.Helpers
{
    public class TestServer
    {
        public void Start(int port)
        {
            TwinoServer server = TwinoServer.CreateWebSocket(async (twinoServer, request, client) =>
            {
                ServerSocket socket = new ServerSocket(twinoServer, request, client);
                return await Task.FromResult(socket);
            });

            server.Start(port);
        }
        
    }
}