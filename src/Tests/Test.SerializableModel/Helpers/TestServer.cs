using System.Linq;
using System.Threading.Tasks;
using Twino.Server;
using Twino.JsonModel;
using Twino.Protocols.WebSocket;

namespace Test.SocketModels.Helpers
{
    public class TestServer
    {
        private readonly int _port;

        public TwinoServer Server { get; private set; }

        public TestServer(int port)
        {
            _port = port;
        }

        public void Run(params PackageReader[] readers)
        {
            ServerOptions options = ServerOptions.CreateDefault();
            options.Hosts.FirstOrDefault().Port = _port;

            Server = new TwinoServer(ServerOptions.CreateDefault());
            Server.UseWebSockets(async (socket, message) =>
            {
                string msg = message.ToString();

                foreach (PackageReader reader in readers)
                    reader.Read(socket, msg);

                await Task.CompletedTask;
            });

            Server.Start(_port);
        }
    }
}