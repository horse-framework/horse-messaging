using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Internal
{
    public class MqConnectionHandler : IProtocolConnectionHandler<TmqMessage>
    {
        private readonly MQServer _server;

        public MqConnectionHandler(MQServer server)
        {
            _server = server;
        }

        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            MqClient client = new MqClient(server, connection, null, true);
            _server.AddClient(client);
            
            throw new System.NotImplementedException();
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            throw new System.NotImplementedException();
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            MqClient mqClient = (MqClient) client;
            _server.RemoveClient(mqClient);
            await Task.CompletedTask;
        }
    }
}