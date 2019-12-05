using System.Threading.Tasks;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Test.Connector.Helpers
{
    public class TestServer
    {
        public void Start(int port)
        {
            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());

            server.UseWebSockets(async delegate { await Task.CompletedTask; });
            server.Start(port);
        }
    }
}