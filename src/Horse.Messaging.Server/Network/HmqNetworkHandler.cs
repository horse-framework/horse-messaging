using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Network
{
    /// <summary>
    /// Message queue server handler
    /// </summary>
    internal class HmqNetworkHandler : IProtocolConnectionHandler<HorseServerSocket, HorseMessage>
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        private readonly INetworkMessageHandler _serverHandler;
        private readonly INetworkMessageHandler _queueMessageHandler;
        private readonly INetworkMessageHandler _routerMessageHandler;
        private readonly INetworkMessageHandler _pullRequestHandler;
        private readonly INetworkMessageHandler _clientHandler;
        private readonly INetworkMessageHandler _responseHandler;
        private readonly INetworkMessageHandler _instanceHandler;
        private readonly INetworkMessageHandler _eventHandler;

        internal INetworkMessageHandler CacheHandler { get; set; }

        public HmqNetworkHandler(HorseMq server)
        {
            _server = server;
            _serverHandler = new ServerMessageHandler(server);
            _queueMessageHandler = new QueueMessageHandler(server);
            _routerMessageHandler = new RouterMessageHandler(server);
            _pullRequestHandler = new PullRequestMessageHandler(server);
            _clientHandler = new DirectMessageHandler(server);
            _responseHandler = new ResponseMessageHandler(server);
            _instanceHandler = new NodeMessageHandler(server);
            _eventHandler = new EventMessageHandler(server);
        }

        #endregion

        #region Connection

        /// <summary>
        /// Called when a new client is connected via HMQ protocol
        /// </summary>
        public async Task<HorseServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(HorseHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MessagingClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(HorseProtocolWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            if (_server.Options.ClientLimit > 0 && _server.GetOnlineClients() >= _server.Options.ClientLimit)
                return null;

            //creates new mq client object 
            MessagingClient client = new MessagingClient(_server, connection, _server.MessageIdGenerator);
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
                    await client.SendAsync(MessageBuilder.Unauthorized());
                    return null;
                }
            }

            //client authenticated, add it into the connected clients list
            _server.AddClient(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            foreach (IClientHandler handler in _server.ClientHandlers)
                _ = handler.Connected(_server, client);

            return client;
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public Task Ready(IHorseServer server, HorseServerSocket client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when connected client is connected in HMQ protocol
        /// </summary>
        public Task Disconnected(IHorseServer server, HorseServerSocket client)
        {
            MessagingClient messagingClient = (MessagingClient) client;
            _server.RemoveClient(messagingClient);
            foreach (IClientHandler handler in _server.ClientHandlers)
                _ = handler.Disconnected(_server, messagingClient);

            return Task.CompletedTask;
        }

        #endregion

        #region Receive

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public Task Received(IHorseServer server, IConnectionInfo info, HorseServerSocket client, HorseMessage message)
        {
            MessagingClient mc = (MessagingClient) client;

            //if client sends anonymous messages and server needs message id, generate new
            if (string.IsNullOrEmpty(message.MessageId))
            {
                //anonymous messages can't be responsed, do not wait response
                if (message.WaitResponse)
                    message.WaitResponse = false;

                message.SetMessageId(_server.MessageIdGenerator.Create());
            }

            //if message does not have a source information, source will be set to sender's unique id
            if (string.IsNullOrEmpty(message.Source))
                message.SetSource(mc.UniqueId);

            //if client sending messages like someone another, kick him
            else if (message.Source != mc.UniqueId)
            {
                client.Disconnect();
                return Task.CompletedTask;
            }

            return RouteToHandler(mc, message, false);
        }

        /// <summary>
        /// Routes message to it's type handler
        /// </summary>
        internal Task RouteToHandler(MessagingClient mc, HorseMessage message, bool fromNode)
        {
            switch (message.Type)
            {
                //client sends a queue message
                case MessageType.QueueMessage:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message, false);
                    return _queueMessageHandler.Handle(mc, message, fromNode);

                //router message
                case MessageType.Router:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message, false);
                    return _routerMessageHandler.Handle(mc, message, fromNode);

                //cache message
                case MessageType.Cache:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message, false);
                    return CacheHandler.Handle(mc, message, fromNode);

                //sends pull request to a queue
                case MessageType.QueuePullRequest:
                    return _pullRequestHandler.Handle(mc, message, fromNode);

                //clients sends a message to another client
                case MessageType.DirectMessage:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message, false);
                    return _clientHandler.Handle(mc, message, fromNode);

                //client sends a response message for a message
                case MessageType.Response:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message, false);
                    return _responseHandler.Handle(mc, message, fromNode);

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    return _serverHandler.Handle(mc, message, fromNode);

                //event subscription or unsubscription request received
                case MessageType.Event:
                    return _eventHandler.Handle(mc, message, fromNode);

                //if client sends a ping message, response with pong
                case MessageType.Ping:
                    return mc.SendAsync(PredefinedMessages.PONG);

                //client sends PONG message
                case MessageType.Pong:
                    mc.KeepAlive();
                    break;

                //close the client's connection
                case MessageType.Terminate:
                    mc.Disconnect();
                    break;
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}