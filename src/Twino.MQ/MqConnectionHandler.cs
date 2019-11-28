using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    internal class MqConnectionHandler : IProtocolConnectionHandler<TmqMessage>
    {
        private readonly MQServer _server;
        private static readonly TmqWriter _writer = new TmqWriter();

        public MqConnectionHandler(MQServer server)
        {
            _server = server;
        }

        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            HeaderMessageBuilder builder = HeaderMessageBuilder.Create();

            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found)
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_BUSY);
                await connection.Socket.SendAsync(await _writer.Create(builder.Get()));
                return null;
            }

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
                {
                    builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_UNAUTHORIZED);
                    await client.SendAsync(builder.Get());
                    return null;
                }
            }

            _server.AddClient(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_ACCEPTED);
            builder.Add(TmqHeaders.CLIENT_ID, client.UniqueId);

            await client.SendAsync(builder.Get());
            return client;
        }

        public Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            //check message target
            //todo: target, another client (if hide client names active, do not process the message)
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