using System.Collections.Immutable;
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
        #region Fields

        private readonly MQServer _server;
        private static readonly TmqWriter _writer = new TmqWriter();

        public MqConnectionHandler(MQServer server)
        {
            _server = server;
        }

        #endregion

        #region Connection

        /// <summary>
        /// Called when a new client is connected via TMQ protocol
        /// </summary>
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found)
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(await _writer.Create(MessageBuilder.Busy()));
                return null;
            }

            //creates new mq client object 
            MqClient client = new MqClient(server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId;
            client.Token = data.Properties.GetStringValue(TmqHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TmqHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TmqHeaders.CLIENT_TYPE);

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
            return client;
        }

        /// <summary>
        /// Called when connected client is connected in TMQ protocol
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            MqClient mqClient = (MqClient) client;
            await _server.RemoveClient(mqClient);
        }

        #endregion

        #region Receive

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
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
                {
                    message.MessageId = _server.MessageIdGenerator.Create();
                    message.SourceLength = message.MessageId.Length;
                }
            }

            //if message does not have a source information, source will be set to sender's unique id
            if (string.IsNullOrEmpty(message.Source))
                message.Source = mc.UniqueId;

            //if client sending messages like someone another, kick him
            else if (message.Source != mc.UniqueId)
            {
                client.Disconnect();
                return;
            }

            switch (message.Type)
            {
                //client sends a queue message in a channel
                case MessageType.Channel:
                    await ChannelMessageReceived(server, mc, message);
                    break;

                //clients sends a message to another client
                case MessageType.Client:
                    await ClientMessageReceived(server, mc, message);
                    break;

                //client sends a delivery message of a message
                case MessageType.Delivery:
                    await DeliveryMessageReceived(server, mc, message);
                    break;

                //client sends a response message for a message
                case MessageType.Response:
                    await ResponseMessageReceived(server, mc, message);
                    break;

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    await ServerMessageReceived(server, mc, message);
                    break;

                //client sends PONG message
                case MessageType.Pong:
                    mc.Pong();
                    break;

                //close the client's connection
                case MessageType.Terminate:
                    mc.Disconnect();
                    break;
            }
        }

        /// <summary>
        /// Clients send a server message
        /// </summary>
        private async Task ServerMessageReceived(ITwinoServer server, MqClient client, TmqMessage message)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Client sends a message to another client
        /// </summary>
        private async Task ClientMessageReceived(ITwinoServer server, MqClient client, TmqMessage message)
        {
            //peer to peer messaging disabled the clients hiding names
            if (_server.Options.HideClientNames)
                await client.SendAsync(MessageBuilder.Unauthorized());

            //find the receiver
            MqClient other = _server.FindClient(message.Target);
            if (other == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //check sending message authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToPeer(client, message, other);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            //send the message
            await other.SendAsync(message);
        }

        /// <summary>
        /// Client sends a message to the queue
        /// </summary>
        private async Task ChannelMessageReceived(ITwinoServer server, MqClient client, TmqMessage message)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);
            if (queue == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //check authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToQueue(client, queue, message);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            //push the message
            QueueMessage queueMessage = new QueueMessage(message);
            await queue.Push(queueMessage);
        }

        /// <summary>
        /// Client sends a delivery message
        /// </summary>
        private async Task DeliveryMessageReceived(ITwinoServer server, MqClient client, TmqMessage message)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
                return;

            ChannelQueue queue = channel.FindQueue(message.ContentType);
            if (queue == null)
                return;

            await queue.MessageDelivered(client, message);
        }

        /// <summary>
        /// Client sends a response message
        /// </summary>
        private async Task ResponseMessageReceived(ITwinoServer server, MqClient client, TmqMessage message)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}