using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;

namespace Twino.MQ.WebSocket.Server
{
    public class TmqOverWsHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
    {
        private readonly TwinoMQ _server;
        private readonly IProtocolConnectionHandler<TmqServerSocket, TwinoMessage> _tmqProtocolHandler;

        public TmqOverWsHandler(TwinoMQ server, IProtocolConnectionHandler<TmqServerSocket, TwinoMessage> tmqProtocolHandler)
        {
            _server = server;
            _tmqProtocolHandler = tmqProtocolHandler;
        }

        public async Task<WsServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(TwinoHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                //todo:
                await connection.Socket.SendAsync(TmqWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            if (_server.Options.ClientLimit > 0 && _server.GetOnlineClients() >= _server.Options.ClientLimit)
                return null;

            //creates new mq over websocket client object
            WsServerSocket socket = new WsServerSocket(server, connection);
            TmqWebSocketClient client = new TmqWebSocketClient(socket, _server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(TwinoHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TwinoHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TwinoHeaders.CLIENT_TYPE);

            //authenticates client
            foreach (IClientAuthenticator authenticator in _server.Authenticators)
            {
                client.IsAuthenticated = await authenticator.Authenticate(_server, client);
                if (!client.IsAuthenticated)
                {
                    //todo:
                    await client.SendAsync(MessageBuilder.Unauthorized());
                    return null;
                }
            }

            //client authenticated, add it into the connected clients list
            _server.AddClient(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            //todo:
            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            foreach (IClientHandler handler in _server.ClientHandlers)
                await handler.Connected(_server, client);

            return socket;
        }

        public async Task Ready(ITwinoServer server, WsServerSocket client)
        {
            throw new System.NotImplementedException();
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
        {
            throw new System.NotImplementedException();
        }

        public async Task Disconnected(ITwinoServer server, WsServerSocket client)
        {
            throw new System.NotImplementedException();
        }
    }
}