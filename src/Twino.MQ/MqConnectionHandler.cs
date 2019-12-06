using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    /// <summary>
    /// Message queue server handler
    /// </summary>
    internal class MqConnectionHandler : IProtocolConnectionHandler<TmqMessage>
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

        public MqConnectionHandler(MqServer server)
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
            MqClient client = new MqClient(_server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId.Trim();
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

            if (_server.ClientHandler != null)
                await _server.ClientHandler.Connected(_server, client);

            return client;
        }

        /// <summary>
        /// Called when connected client is connected in TMQ protocol
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
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
                    message.MessageIdLength = message.MessageId.Length;
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
                    await ChannelMessageReceived(mc, message);
                    break;

                //clients sends a message to another client
                case MessageType.Client:
                    await ClientMessageReceived(mc, message);
                    break;

                //client sends an acknowledge message of a message
                case MessageType.Acknowledge:
                    await AcknowledgeMessageReceived(mc, message);
                    break;

                //client sends a response message for a message
                case MessageType.Response:
                    await ResponseMessageReceived(message);
                    break;

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    await ServerMessageReceived(mc, message);
                    break;

                //client sends PONG message
                case MessageType.Pong:
                    info.PongReceived();
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
        private async Task ServerMessageReceived(MqClient client, TmqMessage message)
        {
            switch (message.ContentType)
            {
                //join to a channel
                case KnownContentTypes.Join:
                    await JoinChannel(client, message);
                    break;

                //leave from a channel
                case KnownContentTypes.Leave:
                    await LeaveChannel(client, message);
                    break;

                //creates new queue
                case KnownContentTypes.CreateQueue:
                    await CreateQueue(client, message);
                    break;
            }
        }

        /// <summary>
        /// Client sends a message to another client
        /// </summary>
        private async Task ClientMessageReceived(MqClient client, TmqMessage message)
        {
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
        private async Task ChannelMessageReceived(MqClient client, TmqMessage message)
        {
            //if client sends this value false accidently, set it true.
            message.FirstAcquirer = true;

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

            //prepare the message
            QueueMessage queueMessage = new QueueMessage(message);
            queueMessage.Source = client;
            message.Source = queue.Channel.Name;
            message.SourceLength = queue.Channel.Name.Length;
            
            //push the message
            await queue.Push(queueMessage, client);
        }

        /// <summary>
        /// Client sends a acknowledge message
        /// </summary>
        private async Task AcknowledgeMessageReceived(MqClient client, TmqMessage message)
        {
            //priority has no role in ack message.
            //we are using priority for helping receiver type recognization for better performance
            if (message.HighPriority)
            {
                //target should be client
                MqClient target = _server.FindClient(message.Target);
                if (target != null)
                {
                    await target.SendAsync(message);
                    return;
                }
            }

            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                //if high prio, dont try to find client again
                if (!message.HighPriority)
                {
                    //target should be client
                    MqClient target = _server.FindClient(message.Target);
                    if (target != null)
                        await target.SendAsync(message);
                }

                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);
            if (queue == null)
                return;

            await queue.AcknowledgeDelivered(client, message);
        }

        /// <summary>
        /// Client sends a response message
        /// </summary>
        private async Task ResponseMessageReceived(TmqMessage message)
        {
            //server does not care response messages
            //if receiver could be found, message is sent to it's receiver
            //if receiver isn't available, response will be thrown

            MqClient receiver = _server.FindClient(message.Target);

            if (receiver != null)
                await receiver.SendAsync(message);
        }

        #endregion

        #region Server Messages

        /// <summary>
        /// Finds and joins to channel and sends response
        /// </summary>
        private async Task JoinChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            bool grant = await channel.AddClient(client);

            if (message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, grant ? KnownContentTypes.Ok : KnownContentTypes.Unauthorized));
        }

        /// <summary>
        /// Leaves from the channel and sends response
        /// </summary>
        private async Task LeaveChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            bool success = await channel.RemoveClient(client);

            if (message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, success ? KnownContentTypes.Ok : KnownContentTypes.NotFound));
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task CreateQueue(MqClient client, TmqMessage message)
        {
            byte[] bytes = new byte[2];
            await message.Content.ReadAsync(bytes);
            ushort contentType = BitConverter.ToUInt16(bytes);

            Channel channel = _server.FindChannel(message.Target);

            //if channel doesn't exists, create new channel
            if (channel == null)
            {
                //check create channel access
                if (_server.Authorization != null)
                {
                    bool grant = await _server.Authorization.CanCreateChannel(client, _server, message.Target);
                    if (!grant)
                    {
                        if (message.ResponseRequired)
                            await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                        return;
                    }
                }

                channel = _server.CreateChannel(message.Target);
            }

            ChannelQueue queue = channel.FindQueue(contentType);

            //if queue exists, we can't create. return duplicate response.
            if (queue != null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Duplicate));

                return;
            }

            //check authority if client can create queue
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateQueue(client, channel, contentType);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return;
                }
            }

            //creates new queue
            queue = await channel.CreateQueue(contentType);

            //if creation successful, sends response
            if (queue != null && message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
        }

        #endregion
    }
}