using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    /// <summary>
    /// Message queue server handler
    /// </summary>
    internal class MqConnectionHandler : IProtocolConnectionHandler<TmqServerSocket, TmqMessage>
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        /// <summary>
        /// Default TMQ protocol message writer
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        private readonly INetworkMessageHandler _serverHandler;
        private readonly INetworkMessageHandler _channelHandler;
        private readonly INetworkMessageHandler _clientHandler;
        private readonly INetworkMessageHandler _responseHandler;
        private readonly INetworkMessageHandler _acknowledgeHandler;
        private readonly INetworkMessageHandler _instanceHandler;

        public MqConnectionHandler(MqServer server)
        {
            _server = server;
            _serverHandler = new ServerMessageHandler(server);
            _channelHandler = new ChannelMessageHandler(server);
            _clientHandler = new ClientMessageHandler(server);
            _responseHandler = new ResponseMessageHandler(server);
            _acknowledgeHandler = new AcknowledgeMessageHandler(server);
            _instanceHandler = new InstanceMessageHandler(server);
        }

        #endregion

        #region Connection

        /// <summary>
        /// Called when a new client is connected via TMQ protocol
        /// </summary>
        public async Task<TmqServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(await _writer.Create(MessageBuilder.Busy()));
                return null;
            }

            //creates new mq client object 
            MqClient client = new MqClient(_server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(TmqHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TmqHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TmqHeaders.CLIENT_TYPE);

            //connecting client is a MQ server 
            string serverValue = data.Properties.GetStringValue(TmqHeaders.TWINO_MQ_SERVER);
            if (!string.IsNullOrEmpty(serverValue) && (serverValue.Equals("1") || serverValue.Equals("true", StringComparison.InvariantCultureIgnoreCase)))
            {
                if (_server.ServerAuthenticator == null)
                    return null;

                bool accepted = await _server.ServerAuthenticator.Authenticate(_server, client);
                if (!accepted)
                    return null;

                client.IsInstanceServer = true;
                await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));
            }

            //connecting client is a producer/consumer client
            else
            {
                //authenticates client
                if (_server.Authenticator != null)
                {
                    client.IsAuthenticated = await _server.Authenticator.Authenticate(_server, client);
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

                if (_server.ClientHandler != null)
                    await _server.ClientHandler.Connected(_server, client);
            }

            return client;
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public async Task Ready(ITwinoServer server, TmqServerSocket client)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called when connected client is connected in TMQ protocol
        /// </summary>
        public async Task Disconnected(ITwinoServer server, TmqServerSocket client)
        {
            MqClient mqClient = (MqClient) client;
            await _server.RemoveClient(mqClient);

            if (_server.ClientHandler != null)
                await _server.ClientHandler.Disconnected(_server, mqClient);
        }

        #endregion

        #region Receive

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, TmqServerSocket client, TmqMessage message)
        {
            MqClient mc = (MqClient) client;

            //if client sends anonymous messages and server needs message id, generate new
            if (string.IsNullOrEmpty(message.MessageId))
            {
                //anonymous messages can't be responsed, do not wait response
                if (message.ResponseRequired)
                    message.ResponseRequired = false;

                //if server want to use message id anyway, generate new.
                if (_server.Options.UseMessageId)
                    message.SetMessageId(_server.MessageIdGenerator.Create());
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

            await RouteToHandler(mc, message, info);
        }

        /// <summary>
        /// Routes message to it's type handler
        /// </summary>
        private async Task RouteToHandler(MqClient mc, TmqMessage message, IConnectionInfo info)
        {
            switch (message.Type)
            {
                //client sends a queue message in a channel
                case MessageType.Channel:
                    await _instanceHandler.Handle(mc, message);
                    await _channelHandler.Handle(mc, message);
                    break;

                //clients sends a message to another client
                case MessageType.Client:
                    await _instanceHandler.Handle(mc, message);
                    await _clientHandler.Handle(mc, message);
                    break;

                //client sends an acknowledge message of a message
                case MessageType.Acknowledge:
                    await _instanceHandler.Handle(mc, message);
                    await _acknowledgeHandler.Handle(mc, message);
                    break;

                //client sends a response message for a message
                case MessageType.Response:
                    await _instanceHandler.Handle(mc, message);
                    await _responseHandler.Handle(mc, message);
                    break;

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    await _serverHandler.Handle(mc, message);
                    break;

                //if client sends a ping message, response with pong
                case MessageType.Ping:
                    await mc.SendAsync(PredefinedMessages.PONG);
                    break;

                //client sends PONG message
                case MessageType.Pong:
                    mc.KeepAlive();
                    break;

                //close the client's connection
                case MessageType.Terminate:
                    mc.Disconnect();
                    break;
            }
        }

        #endregion
    }
}