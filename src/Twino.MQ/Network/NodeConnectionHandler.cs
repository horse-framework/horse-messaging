using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    /// <summary>
    /// Messaging queue node handler
    /// </summary>
    internal class NodeConnectionHandler : IProtocolConnectionHandler<TmqServerSocket, TmqMessage>
    {
        /// <summary>
        /// Default TMQ protocol message writer
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        private readonly NodeServer _server;
        private readonly MqConnectionHandler _connectionHandler;

        /// <summary>
        /// 
        /// </summary>
        internal NodeConnectionHandler(NodeServer server, MqConnectionHandler connectionHandler)
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
                await connection.Socket.SendAsync(await _writer.Create(MessageBuilder.Busy()));
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

            client.RemoteHost = client.Info.Client.Client.RemoteEndPoint.ToString().Split(':')[0];
            _server.Clients.Add(client);

            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            return client;
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task Ready(ITwinoServer server, TmqServerSocket client)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, TmqServerSocket client, TmqMessage message)
        {
            MqClient mc = (MqClient) client;
            await _connectionHandler.RouteToHandler(mc, message, true);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, TmqServerSocket client)
        {
            MqClient node = (MqClient) client;
            _server.Clients.Remove(node);
            await Task.CompletedTask;
        }
    }
}