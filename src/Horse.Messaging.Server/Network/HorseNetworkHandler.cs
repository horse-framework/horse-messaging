using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;
using Horse.Messaging.Server.Transactions;

namespace Horse.Messaging.Server.Network
{
    /// <summary>
    /// Message queue server handler
    /// </summary>
    internal class HorseNetworkHandler : IProtocolConnectionHandler<HorseServerSocket, HorseMessage>
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseRider _rider;

        private readonly INetworkMessageHandler _serverHandler;
        private readonly INetworkMessageHandler _queueMessageHandler;
        private readonly INetworkMessageHandler _routerMessageHandler;
        private readonly INetworkMessageHandler _pullRequestHandler;
        private readonly INetworkMessageHandler _clientHandler;
        private readonly INetworkMessageHandler _responseHandler;
        private readonly INetworkMessageHandler _channelHandler;
        private readonly INetworkMessageHandler _eventHandler;
        private readonly INetworkMessageHandler _cacheHandler;
        private readonly INetworkMessageHandler _transactionHandler;

        public HorseNetworkHandler(HorseRider rider)
        {
            _rider = rider;
            _serverHandler = new ServerMessageHandler(rider);
            _queueMessageHandler = new QueueMessageHandler(rider);
            _routerMessageHandler = new RouterMessageHandler(rider);
            _pullRequestHandler = new PullRequestMessageHandler(rider);
            _clientHandler = new DirectMessageHandler(rider);
            _responseHandler = new ResponseMessageHandler(rider);
            _eventHandler = new EventMessageHandler(rider);
            _cacheHandler = new CacheNetworkHandler(rider);
            _channelHandler = new ChannelNetworkHandler(rider);
            _transactionHandler = new TransactionMessageHandler(rider);
        }

        #endregion

        #region Connection

        /// <summary>
        /// Called when a new client is connected via Horse protocol
        /// </summary>
        public async Task<HorseServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(HorseHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _rider.Client.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MessagingClient foundClient = _rider.Client.Find(clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(HorseProtocolWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            string nodeValue = data.Properties.GetStringValue(HorseHeaders.HORSE_NODE);
            bool isNode = nodeValue != null && nodeValue.Equals(HorseHeaders.YES, StringComparison.InvariantCultureIgnoreCase);

            if (!isNode && _rider.Options.ClientLimit > 0 && _rider.Client.GetOnlineClients() >= _rider.Options.ClientLimit)
                return null;

            //creates new mq client object 
            MessagingClient client = new MessagingClient(_rider, connection, _rider.MessageIdGenerator);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(HorseHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(HorseHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(HorseHeaders.CLIENT_TYPE);

            if (isNode)
            {
                if (_rider.Cluster.Options.SharedSecret != client.Token)
                {
                    await client.SendAsync(MessageBuilder.Unauthorized());
                    return null;
                }

                NodeClient nodeClient = _rider.Cluster.Clients.FirstOrDefault(x => x.Info.Name == client.Name);
                if (nodeClient == null)
                {
                    await client.SendAsync(MessageBuilder.NotFound());
                    return null;
                }

                client.IsNodeClient = true;
                client.NodeClient = nodeClient;

                return client;
            }

            //authenticates client
            foreach (IClientAuthenticator authenticator in _rider.Client.Authenticators.All())
            {
                client.IsAuthenticated = await authenticator.Authenticate(_rider, client);
                if (!client.IsAuthenticated)
                {
                    await client.SendAsync(MessageBuilder.Unauthorized());
                    return null;
                }
            }

            if (!_rider.Cluster.CanClientConnect())
                return null;

            if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State > NodeState.Main)
            {
                foreach (IClientHandler handler in _rider.Client.Handlers.All())
                    _ = handler.Connected(_rider, client);

                return client;
            }

            //client authenticated, add it into the connected clients list
            _rider.Client.Add(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            HorseMessage accepted = MessageBuilder.Accepted(client.UniqueId);

            if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State <= NodeState.Main)
            {
                if (_rider.Cluster.SuccessorNode?.PublicHost != null)
                    accepted.AddHeader(HorseHeaders.SUCCESSOR_NODE, _rider.Cluster.SuccessorNode.PublicHost);

                string alternate = string.Empty;
                foreach (NodeClient nodeClient in _rider.Cluster.Clients)
                {
                    if (!nodeClient.IsConnected)
                        continue;

                    if (nodeClient.Info.Id == _rider.Cluster.SuccessorNode?.Id)
                        continue;

                    alternate += alternate.Length == 0 ? nodeClient.Info.PublicHost : $",{nodeClient.Info.PublicHost}";
                }

                if (!string.IsNullOrEmpty(alternate))
                    accepted.AddHeader(HorseHeaders.REPLICA_NODE, alternate);
            }

            await client.SendAsync(accepted);

            foreach (IClientHandler handler in _rider.Client.Handlers.All())
                _ = handler.Connected(_rider, client);

            return client;
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public async Task Ready(IHorseServer server, HorseServerSocket client)
        {
            if (client == null)
                return;
            
            MessagingClient mc = (MessagingClient) client;

            if (mc.IsNodeClient && mc.NodeClient != null)
            {
                mc.NodeClient.IncomingClientConnected(mc, mc.Data);
                return;
            }
            
            if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State > NodeState.Main)
            {
                HorseMessage message = MessageBuilder.StatusCodeMessage(KnownContentTypes.Found, mc.UniqueId);
                if (_rider.Cluster.MainNode != null)
                {
                    message.AddHeader(HorseHeaders.NODE_ID, _rider.Cluster.MainNode.Id);
                    message.AddHeader(HorseHeaders.NODE_NAME, _rider.Cluster.MainNode.Name);
                    message.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, _rider.Cluster.MainNode.PublicHost);

                    if (_rider.Cluster.SuccessorNode != null)
                        message.AddHeader(HorseHeaders.SUCCESSOR_NODE, _rider.Cluster.SuccessorNode.PublicHost);
                }

                await client.SendAsync(message);
                client.Disconnect();
            }
        }

        /// <summary>
        /// Called when connected client is connected in Horse protocol
        /// </summary>
        public Task Disconnected(IHorseServer server, HorseServerSocket client)
        {
            MessagingClient messagingClient = (MessagingClient) client;

            if (messagingClient.IsNodeClient)
            {
                return Task.CompletedTask;
            }

            _rider.Client.Remove(messagingClient);
            foreach (IClientHandler handler in _rider.Client.Handlers.All())
                _ = handler.Disconnected(_rider, messagingClient);

            return Task.CompletedTask;
        }

        #endregion

        #region Receive

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public async Task Received(IHorseServer server, IConnectionInfo info, HorseServerSocket client, HorseMessage message)
        {
            try
            {
                MessagingClient mc = (MessagingClient) client;

                //if client sends anonymous messages and server needs message id, generate new
                if (string.IsNullOrEmpty(message.MessageId))
                {
                    //anonymous messages can't be responsed, do not wait response
                    if (message.WaitResponse)
                        message.WaitResponse = false;

                    message.SetMessageId(_rider.MessageIdGenerator.Create());
                }

                //if message does not have a source information, source will be set to sender's unique id
                if (string.IsNullOrEmpty(message.Source))
                    message.SetSource(mc.UniqueId);

                //if client sending messages like someone another, kick him
                else if (message.Source != mc.UniqueId)
                {
                    client.Disconnect();
                    return;
                }

                await RouteToHandler(mc, message, false);
            }
            catch
            {
                client.Disconnect();
            }
        }

        /// <summary>
        /// Routes message to it's type handler
        /// </summary>
        internal Task RouteToHandler(MessagingClient mc, HorseMessage message, bool fromNode)
        {
            ClusterMode clusterMode = _rider.Cluster.Options.Mode;
            bool isReplica = _rider.Cluster.Options.Mode == ClusterMode.Reliable &&
                             (_rider.Cluster.State == NodeState.Replica || _rider.Cluster.State == NodeState.Successor);

            switch (message.Type)
            {
                case MessageType.Channel:
                    if (!fromNode && clusterMode == ClusterMode.Scaled)
                        _rider.Cluster.ProcessMessageFromClient(mc, message);

                    if (isReplica)
                        return Task.CompletedTask;

                    return _channelHandler.Handle(mc, message, fromNode);

                case MessageType.QueueMessage:

                    if (isReplica)
                        return Task.CompletedTask;

                    return _queueMessageHandler.Handle(mc, message, fromNode);

                case MessageType.Router:

                    if (isReplica)
                        return Task.CompletedTask;

                    return _routerMessageHandler.Handle(mc, message, fromNode);

                case MessageType.Cache:

                    if (!fromNode && clusterMode == ClusterMode.Scaled)
                        _rider.Cluster.ProcessMessageFromClient(mc, message);

                    if (isReplica)
                        return Task.CompletedTask;

                    return _cacheHandler.Handle(mc, message, fromNode);

                case MessageType.Transaction:

                    if (isReplica)
                        return Task.CompletedTask;

                    return _transactionHandler.Handle(mc, message, false);

                case MessageType.QueuePullRequest:

                    if (isReplica)
                        return Task.CompletedTask;

                    return _pullRequestHandler.Handle(mc, message, fromNode);

                case MessageType.DirectMessage:

                    if (!fromNode && clusterMode == ClusterMode.Scaled)
                        _rider.Cluster.ProcessMessageFromClient(mc, message);

                    if (isReplica)
                        return Task.CompletedTask;

                    return _clientHandler.Handle(mc, message, fromNode);

                case MessageType.Response:
                    if (!fromNode && clusterMode == ClusterMode.Scaled)
                        _rider.Cluster.ProcessMessageFromClient(mc, message);

                    if (clusterMode == ClusterMode.Reliable && mc.IsNodeClient && mc.NodeClient != null)
                        mc.NodeClient.ProcessReceivedMessage(mc, message);

                    if (isReplica)
                        return Task.CompletedTask;

                    return _responseHandler.Handle(mc, message, fromNode);

                case MessageType.Server:
                    return _serverHandler.Handle(mc, message, fromNode);

                case MessageType.Cluster:
                    if (mc.IsNodeClient && mc.NodeClient != null)
                        mc.NodeClient.ProcessReceivedMessage(mc, message);

                    return Task.CompletedTask;

                case MessageType.Event:
                    return _eventHandler.Handle(mc, message, fromNode);

                case MessageType.Ping:
                    return mc.SendAsync(PredefinedMessages.PONG);

                case MessageType.Pong:
                    mc.KeepAlive();
                    break;

                case MessageType.Terminate:
                    mc.Disconnect();
                    break;
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}