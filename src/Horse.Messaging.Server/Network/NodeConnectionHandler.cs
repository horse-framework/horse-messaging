using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Network
{
    /// <summary>
    /// Messaging queue node handler
    /// </summary>
    internal class NodeConnectionHandler : IProtocolConnectionHandler<HorseServerSocket, HorseMessage>
    {
        private readonly NodeManager _server;
        private readonly HmqNetworkHandler _connectionHandler;

        /// <summary>
        /// 
        /// </summary>
        internal NodeConnectionHandler(NodeManager server, HmqNetworkHandler connectionHandler)
        {
            _server = server;
            _connectionHandler = connectionHandler;
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<HorseServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(HorseHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.Self.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MessagingClient foundClient = _server.IncomingNodes.Find(x => x.UniqueId == clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(HorseProtocolWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            //creates new node client object 
            MessagingClient client = new MessagingClient(_server.Self, connection);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(HorseHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(HorseHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(HorseHeaders.CLIENT_TYPE);

            if (_server.Authenticator != null)
            {
                bool accepted = await _server.Authenticator.Authenticate(_server, client);
                if (!accepted)
                    return null;
            }

            client.RemoteHost = client.Info.Client.Client.RemoteEndPoint.ToString()?.Split(':')[0];
            _server.IncomingNodes.Add(client);

            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            return client;
        }

        /// <summary>
        /// 
        /// </summary>
        public Task Ready(IHorseServer server, HorseServerSocket client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public Task Received(IHorseServer server, IConnectionInfo info, HorseServerSocket client, HorseMessage message)
        {
            MessagingClient mc = (MessagingClient) client;
            if (message.Type == MessageType.Server)
            {
                if (message.ContentType == KnownContentTypes.DecisionOverNode)
                {
                    DecisionOverNode(message);
                    return Task.CompletedTask;
                }
            }

            return _connectionHandler.RouteToHandler(mc, message, true);
        }

        /// <summary>
        /// 
        /// </summary>
        public Task Disconnected(IHorseServer server, HorseServerSocket client)
        {
            MessagingClient node = (MessagingClient) client;
            _server.IncomingNodes.Remove(node);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads decision and applies it
        /// </summary>
        private void DecisionOverNode(HorseMessage message)
        {
            DecisionOverNode model = message.Deserialize<DecisionOverNode>(_server.Self.MessageContentSerializer);
            if (model == null)
                return;

            HorseQueue queue = _server.Self.FindQueue(model.Queue);
            if (queue == null)
                return;

            Decision decision = new Decision(model.Allow, model.SaveMessage, model.PutBack, model.Acknowledge);
            _ = queue.ApplyDecisionOverNode(model.MessageId, decision);
        }
    }
}