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
        private readonly MQServer _server;
        private static readonly TmqWriter _writer = new TmqWriter();

        public MqConnectionHandler(MQServer server)
        {
            _server = server;
        }

        /// <summary>
        /// Called when a new client is connected via TMQ protocol
        /// </summary>
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            HeaderMessageBuilder builder = HeaderMessageBuilder.Create();

            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found)
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_BUSY);
                await connection.Socket.SendAsync(await _writer.Create(builder.Get()));
                return null;
            }

            //creates new mq client object 
            MqClient client = new MqClient(server, connection, _server.MessageIdGenerator, true);
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
                    builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_UNAUTHORIZED);
                    await client.SendAsync(builder.Get());
                    return null;
                }
            }

            //client authenticated, add it into the connected clients list
            _server.AddClient(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            builder.Add(TmqHeaders.CLIENT_ACCEPT, TmqHeaders.VALUE_ACCEPTED);
            builder.Add(TmqHeaders.CLIENT_ID, client.UniqueId);

            await client.SendAsync(builder.Get());
            return client;
        }

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            //check message target
            //todo: target, another client (if hide client names active, do not process the message)
            //todo: target, channel
            //todo: target, server

            switch (message.Type)
            {
                //client sends a queue message in a channel
                case MessageType.Channel:
                    break;

                //clients sends a message to another client
                case MessageType.Client:
                    break;

                //client sends a delivery message of a message
                case MessageType.Delivery:
                    break;

                //client sends PONG message
                case MessageType.Pong:
                    break;

                //client sends a response message for a message
                case MessageType.Response:
                    break;

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    break;

                //close the client's connection
                case MessageType.Terminate:
                    break;

                //PING messages are sent from servers, not from clients
                //case MessageType.Ping: break;

                //Twino.MQ does not support redirection
                //case MessageType.Redirect: break;

                //Twino.MQ does not support other message types
                //case MessageType.Other: break;
            }

            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Called when connected client is connected in TMQ protocol
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            MqClient mqClient = (MqClient) client;
            await _server.RemoveClient(mqClient);
        }
    }
}