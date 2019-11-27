using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ
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
            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found)
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
                return null;

            MqClient client = new MqClient(server, connection, _server.MessageIdGenerator, true);
            client.Data = data;
            client.UniqueId = clientId;
            client.Token = data.Properties.GetStringValue(TmqHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TmqHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TmqHeaders.CLIENT_TYPE);

            if (_server.Authenticator != null)
            {
                client.IsAuthenticated = await _server.Authenticator.Authenticate(_server, client);
                if (!client.IsAuthenticated)
                    return null;
            }

            _server.AddClient(client);

            //todo:
            //send client info to client as response
            //if client's unique id has changed, client must be notified

            return client;
        }

        public Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            //check message target
            //todo: target, another client
            //todo: target, channel
            //todo: target, server
            
            throw new System.NotImplementedException();
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            MqClient mqClient = (MqClient) client;
            await _server.RemoveClient(mqClient);
        }
    }
}