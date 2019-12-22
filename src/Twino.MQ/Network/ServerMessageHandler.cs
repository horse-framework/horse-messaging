using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ServerMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public ServerMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion


        public async Task Handle(MqClient client, TmqMessage message)
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

                //create only channel
                case KnownContentTypes.CreateChannel:
                    break;

                //remove channel and queues in it
                case KnownContentTypes.RemoveChannel:
                    await RemoveChannel(client, message);
                    break;

                //creates new queue
                case KnownContentTypes.CreateQueue:
                    await CreateQueue(client, message);
                    break;

                //remove only queue
                case KnownContentTypes.RemoveQueue:
                    await RemoveQueue(client, message);
                    break;

                //update queue
                case KnownContentTypes.UpdateQueue:
                    await UpdateQueue(client, message);
                    break;
            }
        }

        #region Channel

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
        /// Creates new channel
        /// </summary>
        private async Task<Channel> CreateChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel != null)
                return channel;

            //check create channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateChannel(client, _server, message.Target);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return null;
                }
            }

            return _server.CreateChannel(message.Target);
        }

        /// <summary>
        /// Removes a channel with it's queues
        /// </summary>
        private async Task RemoveChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //check remove channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanRemoveChannel(client, _server, channel);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return;
                }
            }

            await _server.RemoveChannel(channel);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task CreateQueue(MqClient client, TmqMessage message)
        {
            ushort? contentType;
            QueueOptionsBuilder builder = null;
            if (message.Length == 2)
            {
                byte[] bytes = new byte[2];
                await message.Content.ReadAsync(bytes);
                contentType = BitConverter.ToUInt16(bytes);
            }
            else
            {
                builder = new QueueOptionsBuilder();
                builder.Load(message.ToString());
                contentType = builder.ContentType;
            }

            Channel channel = await CreateChannel(client, message);
            ChannelQueue queue = channel.FindQueue(contentType.Value);

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
                bool grant = await _server.Authorization.CanCreateQueue(client, channel, contentType.Value, builder);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return;
                }
            }

            //creates new queue
            ChannelQueueOptions options = (ChannelQueueOptions) channel.Options.Clone();
            if (builder != null)
                builder.ApplyTo(options);

            queue = await channel.CreateQueue(contentType.Value, options);

            //if creation successful, sends response
            if (queue != null && message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
        }

        /// <summary>
        /// Removes a queue from a channel
        /// </summary>
        private async Task RemoveQueue(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            byte[] bytes = new byte[2];
            await message.Content.ReadAsync(bytes);
            ushort contentType = BitConverter.ToUInt16(bytes);

            ChannelQueue queue = channel.FindQueue(contentType);
            if (queue == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //check authority if client can create queue
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanRemoveQueue(client, queue);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return;
                }
            }

            await channel.RemoveQueue(queue);
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task UpdateQueue(MqClient client, TmqMessage message)
        {
            QueueOptionsBuilder builder = new QueueOptionsBuilder();
            builder.Load(message.ToString());

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            ChannelQueue queue = channel.FindQueue(builder.ContentType);
            if (queue == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            //check authority if client can create queue
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanUpdateQueueOptions(client, channel, queue, builder);
                if (!grant)
                {
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return;
                }
            }

            builder.ApplyTo(queue.Options);
            if (builder.Status.HasValue)
                await queue.SetStatus(builder.Status.Value);

            //if creation successful, sends response
            if (message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
        }

        #endregion
    }
}