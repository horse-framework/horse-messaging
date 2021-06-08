using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Mq.Clients;
using Horse.Mq.Helpers;
using Horse.Mq.Security;
using Horse.Protocols.Hmq;
using Horse.Protocols.WebSocket;

namespace Horse.Mq.WebSocket.Server
{
    public class HmqOverWsHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
    {
        private readonly HorseMq _server;
        private readonly IProtocolConnectionHandler<HorseServerSocket, HorseMessage> _tmqProtocolHandler;

        public HmqOverWsHandler(HorseMq server, IProtocolConnectionHandler<HorseServerSocket, HorseMessage> tmqProtocolHandler)
        {
            _server = server;
            _tmqProtocolHandler = tmqProtocolHandler;
        }

        public async Task<WsServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(HorseHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                //todo:
                await connection.Socket.SendAsync(HmqWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            if (_server.Options.ClientLimit > 0 && _server.GetOnlineClients() >= _server.Options.ClientLimit)
                return null;

            //creates new mq over websocket client object
            WsServerSocket socket = new WsServerSocket(server, connection);
            HmqWebSocketClient client = new HmqWebSocketClient(socket, _server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(HorseHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(HorseHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(HorseHeaders.CLIENT_TYPE);

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

        public async Task Ready(IHorseServer server, WsServerSocket client)
        {
            throw new System.NotImplementedException();
        }

        public async Task Received(IHorseServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
        {
            throw new System.NotImplementedException();
        }

        public async Task Disconnected(IHorseServer server, WsServerSocket client)
        {
            throw new System.NotImplementedException();
        }
    }
}