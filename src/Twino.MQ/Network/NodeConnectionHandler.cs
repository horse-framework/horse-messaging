using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    /// <summary>
    /// Messaging queue node handler
    /// </summary>
    internal class NodeConnectionHandler : IProtocolConnectionHandler<TmqServerSocket, TwinoMessage>
    {
        private readonly NodeManager _server;
        private readonly NetworkMessageHandler _connectionHandler;

        /// <summary>
        /// 
        /// </summary>
        internal NodeConnectionHandler(NodeManager server, NetworkMessageHandler connectionHandler)
        {
            _server = server;
            _connectionHandler = connectionHandler;
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<TmqServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.Server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.Clients.Find(x => x.UniqueId == clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(TmqWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            //creates new node client object 
            MqClient client = new MqClient(_server.Server, connection);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(TmqHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TmqHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TmqHeaders.CLIENT_TYPE);

            if (_server.Authenticator != null)
            {
                bool accepted = await _server.Authenticator.Authenticate(_server, client);
                if (!accepted)
                    return null;
            }

            client.RemoteHost = client.Info.Client.Client.RemoteEndPoint.ToString()?.Split(':')[0];
            _server.Clients.Add(client);

            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            return client;
        }

        /// <summary>
        /// 
        /// </summary>
        public Task Ready(ITwinoServer server, TmqServerSocket client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public Task Received(ITwinoServer server, IConnectionInfo info, TmqServerSocket client, TwinoMessage message)
        {
            MqClient mc = (MqClient) client;
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
        public Task Disconnected(ITwinoServer server, TmqServerSocket client)
        {
            MqClient node = (MqClient) client;
            _server.Clients.Remove(node);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads decision and applies it
        /// </summary>
        private void DecisionOverNode(TwinoMessage message)
        {
            DecisionOverNode model = message.Deserialize<DecisionOverNode>(_server.Server.MessageContentSerializer);
            if (model == null)
                return;

            Channel channel = _server.Server.FindChannel(model.Channel);
            if (channel == null)
                return;

            TwinoQueue queue = channel.FindQueue(model.Queue);
            if (queue == null)
                return;

            Decision decision = new Decision(model.Allow, model.SaveMessage, model.PutBack, model.Acknowledge);
            _ = queue.ApplyDecisionOverNode(model.MessageId, decision);
        }
    }
}