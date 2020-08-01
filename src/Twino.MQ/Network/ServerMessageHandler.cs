using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

namespace Twino.MQ.Network
{
    internal class ServerMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly TwinoMQ _server;

        public ServerMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        #region Handle

        public Task Handle(MqClient client, TmqMessage message, bool fromNode)
        {
            try
            {
                return HandleUnsafe(client, message);
            }
            catch (OperationCanceledException)
            {
                return client.SendAsync(message.CreateResponse(TwinoResultCode.LimitExceeded));
            }
            catch (DuplicateNameException)
            {
                return client.SendAsync(message.CreateResponse(TwinoResultCode.Duplicate));
            }
        }

        private Task HandleUnsafe(MqClient client, TmqMessage message)
        {
            switch (message.ContentType)
            {
                //join to a channel
                case KnownContentTypes.Join:
                    return JoinChannel(client, message);

                //leave from a channel
                case KnownContentTypes.Leave:
                    return LeaveChannel(client, message);

                //create only channel
                case KnownContentTypes.CreateChannel:
                    return CreateChannel(client, message, false);

                //get channel information list
                case KnownContentTypes.ChannelList:
                    return GetChannelList(client, message);

                //get channel information
                case KnownContentTypes.ChannelInformation:
                    return GetChannelInformation(client, message);

                //get channel consumers
                case KnownContentTypes.ChannelConsumers:
                    return GetChannelConsumers(client, message);

                //remove channel and queues in it
                case KnownContentTypes.RemoveChannel:
                    return RemoveChannel(client, message);

                //creates new queue
                case KnownContentTypes.CreateQueue:
                    return CreateQueue(client, message);

                //remove only queue
                case KnownContentTypes.RemoveQueue:
                    return RemoveQueue(client, message);

                //update queue
                case KnownContentTypes.UpdateQueue:
                    return UpdateQueue(client, message);

                //clear messages in queue
                case KnownContentTypes.ClearMessages:
                    return ClearMessages(client, message);

                //get queue information list
                case KnownContentTypes.QueueList:
                    return GetQueueList(client, message);

                //get queue information
                case KnownContentTypes.QueueInformation:
                    return GetQueueInformation(client, message);

                //get queue information
                case KnownContentTypes.InstanceList:
                    return GetInstanceList(client, message);

                //get client information list
                case KnownContentTypes.ClientList:
                    return GetClients(client, message);

                //for not-defines content types, use user-defined message handler
                default:
                    if (_server.ServerMessageHandler != null)
                        return _server.ServerMessageHandler.Received(client, message);

                    return Task.CompletedTask;
            }
        }

        #endregion

        #region Channel

        /// <summary>
        /// Finds and joins to channel and sends response
        /// </summary>
        private async Task JoinChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            ChannelClient found = channel.FindClient(client.UniqueId);
            if (found != null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));

                return;
            }

            ClientJoinResult result = await channel.AddClient(client);
            if (message.PendingResponse)
            {
                switch (result)
                {
                    case ClientJoinResult.Success:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
                        break;

                    case ClientJoinResult.Unauthorized:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                        break;

                    case ClientJoinResult.Full:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.LimitExceeded));
                        break;
                }
            }
        }

        /// <summary>
        /// Leaves from the channel and sends response
        /// </summary>
        private async Task LeaveChannel(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            bool success = await channel.RemoveClient(client);

            if (message.PendingResponse)
                await client.SendAsync(message.CreateResponse(success ? TwinoResultCode.Ok : TwinoResultCode.NotFound));
        }

        /// <summary>
        /// Creates new channel
        /// </summary>
        private async Task<Channel> CreateChannel(MqClient client, TmqMessage message, bool createForQueue)
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
                    if (message.PendingResponse)
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                    return null;
                }
            }

            if (!createForQueue && message.Length > 0 && message.Content != null && message.Content.Length > 0)
            {
                NetworkOptionsBuilder builder = await System.Text.Json.JsonSerializer.DeserializeAsync<NetworkOptionsBuilder>(message.Content);

                ChannelOptions options = ChannelOptions.CloneFrom(_server.Options);
                builder.ApplyToChannel(options);

                Channel ch = _server.CreateChannel(message.Target, options);

                if (ch != null && message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
            }

            Channel c = _server.CreateChannel(message.Target);

            if (!createForQueue && c != null && message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));

            return c;
        }

        /// <summary>
        /// Removes a channel with it's queues
        /// </summary>
        private async Task RemoveChannel(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanRemoveChannel(client, _server, channel);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            await _server.RemoveChannel(channel);

            if (message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Finds the channel and sends the information
        /// </summary>
        private async Task GetChannelList(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            string filter = message.FindHeader(TmqHeaders.CHANNEL_NAME);

            List<ChannelInformation> list = new List<ChannelInformation>();
            foreach (Channel channel in _server.Channels)
            {
                if (channel == null)
                    continue;

                if (filter != null && !Filter.CheckMatch(channel.Name, filter))
                    continue;

                bool grant = await _server.AdminAuthorization.CanReceiveChannelInfo(client, channel);
                if (!grant)
                    continue;

                list.Add(new ChannelInformation
                         {
                             Name = channel.Name,
                             Queues = channel.QueuesClone.Select(x => x.Id).ToArray(),
                             AllowMultipleQueues = channel.Options.AllowMultipleQueues,
                             AllowedQueues = channel.Options.AllowedQueues,
                             OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                             RequestAcknowledge = channel.Options.RequestAcknowledge,
                             AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                             MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                             WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                             HideClientNames = channel.Options.HideClientNames,
                             QueueLimit = channel.Options.QueueLimit,
                             ClientLimit = channel.Options.ClientLimit,
                             DestroyWhenEmpty = channel.Options.DestroyWhenEmpty,
                             ActiveClients = channel.ClientsCount()
                         });
            }

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.ChannelList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Finds the channel and sends the information
        /// </summary>
        private async Task GetChannelInformation(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveChannelInfo(client, channel);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            ChannelInformation information = new ChannelInformation
                                             {
                                                 Name = channel.Name,
                                                 Queues = channel.QueuesClone.Select(x => x.Id).ToArray(),
                                                 AllowMultipleQueues = channel.Options.AllowMultipleQueues,
                                                 AllowedQueues = channel.Options.AllowedQueues,
                                                 OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                                                 RequestAcknowledge = channel.Options.RequestAcknowledge,
                                                 AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                                                 MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                                                 WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                                                 HideClientNames = channel.Options.HideClientNames,
                                                 QueueLimit = channel.Options.QueueLimit,
                                                 ClientLimit = channel.Options.ClientLimit,
                                                 DestroyWhenEmpty = channel.Options.DestroyWhenEmpty,
                                                 ActiveClients = channel.ClientsCount()
                                             };

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.ChannelInformation;
            response.Serialize(information, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Gets active consumers of channel 
        /// </summary>
        public async Task GetChannelConsumers(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveChannelConsumers(client, channel);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<ClientInformation> list = new List<ClientInformation>();

            foreach (ChannelClient cc in channel.ClientsClone)
                list.Add(new ClientInformation
                         {
                             Id = cc.Client.UniqueId,
                             Name = cc.Client.Name,
                             Type = cc.Client.Type,
                             IsAuthenticated = cc.Client.IsAuthenticated,
                             Online = cc.JoinDate.LifetimeMilliseconds(),
                         });

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.ChannelConsumers;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task CreateQueue(MqClient client, TmqMessage message)
        {
            string queueId = message.FindHeader(TmqHeaders.QUEUE_ID);
            if (string.IsNullOrEmpty(queueId))
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Unacceptable));

            ushort contentType = Convert.ToUInt16(queueId);

            NetworkOptionsBuilder builder = null;
            if (message.Length > 0)
                builder = await System.Text.Json.JsonSerializer.DeserializeAsync<NetworkOptionsBuilder>(message.Content);

            Channel channel = await CreateChannel(client, message, true);
            ChannelQueue queue = channel.FindQueue(contentType);

            //if queue exists, we can't create. return duplicate response.
            if (queue != null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Duplicate));

                return;
            }

            //check authority if client can create queue
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateQueue(client, channel, contentType, builder);
                if (!grant)
                {
                    if (message.PendingResponse)
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                    return;
                }
            }

            //creates new queue
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            queue = await channel.CreateQueue(contentType, options, message, channel.Server.DeliveryHandlerFactory);

            //if creation successful, sends response
            if (queue != null && message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Removes a queue from a channel
        /// </summary>
        private async Task RemoveQueue(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            byte[] bytes = new byte[2];
            await message.Content.ReadAsync(bytes);
            ushort contentType = BitConverter.ToUInt16(bytes);

            ChannelQueue queue = channel.FindQueue(contentType);
            if (queue == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanRemoveQueue(client, queue);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            await channel.RemoveQueue(queue);

            if (message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task UpdateQueue(MqClient client, TmqMessage message)
        {
            string queueId = message.FindHeader(TmqHeaders.QUEUE_ID);
            if (string.IsNullOrEmpty(queueId))
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Unacceptable));

            ushort contentType = Convert.ToUInt16(queueId);

            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            NetworkOptionsBuilder builder = await System.Text.Json.JsonSerializer.DeserializeAsync<NetworkOptionsBuilder>(message.Content);

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            ChannelQueue queue = channel.FindQueue(contentType);
            if (queue == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanUpdateQueueOptions(client, channel, queue, builder);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            builder.ApplyToQueue(queue.Options);
            if (builder.Status.HasValue)
                await queue.SetStatus(builder.Status.Value);

            channel.OnQueueUpdated.Trigger(queue);

            //if creation successful, sends response
            if (message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Clears messages in a queue
        /// </summary>
        private async Task ClearMessages(MqClient client, TmqMessage message)
        {
            string queueId = message.FindHeader(TmqHeaders.QUEUE_ID);
            if (string.IsNullOrEmpty(queueId))
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Unacceptable));

            ushort contentType = Convert.ToUInt16(queueId);

            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            ChannelQueue queue = channel.FindQueue(contentType);
            if (queue == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            string prio = message.FindHeader(TmqHeaders.PRIORITY_MESSAGES);
            string msgs = message.FindHeader(TmqHeaders.MESSAGES);
            bool clearPrio = !string.IsNullOrEmpty(prio) && prio.Equals("yes", StringComparison.InvariantCultureIgnoreCase);
            bool clearMsgs = !string.IsNullOrEmpty(msgs) && msgs.Equals("yes", StringComparison.InvariantCultureIgnoreCase);

            bool grant = await _server.AdminAuthorization.CanClearQueueMessages(client, queue, clearPrio, clearMsgs);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            if (clearPrio && clearMsgs)
                queue.ClearAllMessages();
            else if (clearPrio)
                queue.ClearHighPriorityMessages();
            else if (clearMsgs)
                queue.ClearRegularMessages();

            //if creation successful, sends response
            if (message.PendingResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        private async Task GetQueueList(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveChannelQueues(client, channel);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<QueueInformation> list = new List<QueueInformation>();
            foreach (ChannelQueue queue in channel.QueuesClone)
            {
                if (queue == null)
                    continue;

                list.Add(new QueueInformation
                         {
                             Channel = channel.Name,
                             TagName = queue.TagName,
                             Id = queue.Id,
                             Status = queue.Status.ToString().ToLower(),
                             PriorityMessages = queue.PriorityMessagesList.Count,
                             Messages = queue.MessagesList.Count,
                             OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                             RequestAcknowledge = channel.Options.RequestAcknowledge,
                             AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                             MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                             WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                             HideClientNames = channel.Options.HideClientNames,
                             ReceivedMessages = queue.Info.ReceivedMessages,
                             SentMessages = queue.Info.SentMessages,
                             Deliveries = queue.Info.Deliveries,
                             NegativeAcks = queue.Info.NegativeAcknowledge,
                             Acks = queue.Info.Acknowledges,
                             TimeoutMessages = queue.Info.TimedOutMessages,
                             SavedMessages = queue.Info.MessageSaved,
                             RemovedMessages = queue.Info.MessageRemoved,
                             Errors = queue.Info.ErrorCount,
                             LastMessageReceived = queue.Info.GetLastMessageReceiveUnix(),
                             LastMessageSent = queue.Info.GetLastMessageSendUnix(),
                             MessageLimit = queue.Options.MessageLimit,
                             MessageSizeLimit = queue.Options.MessageSizeLimit
                         });
            }

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.QueueList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Finds the queue and sends the information
        /// </summary>
        private async Task GetQueueInformation(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveChannelQueues(client, channel);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            byte[] bytes = new byte[2];
            await message.Content.ReadAsync(bytes);
            ushort id = BitConverter.ToUInt16(bytes);
            ChannelQueue queue = channel.FindQueue(id);
            if (queue == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            QueueInformation information = new QueueInformation
                                           {
                                               Channel = channel.Name,
                                               TagName = queue.TagName,
                                               Id = id,
                                               Status = queue.Status.ToString().ToLower(),
                                               PriorityMessages = queue.PriorityMessagesList.Count,
                                               Messages = queue.MessagesList.Count,
                                               OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                                               RequestAcknowledge = channel.Options.RequestAcknowledge,
                                               AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                                               MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                                               WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                                               HideClientNames = channel.Options.HideClientNames,
                                               ReceivedMessages = queue.Info.ReceivedMessages,
                                               SentMessages = queue.Info.SentMessages,
                                               Deliveries = queue.Info.Deliveries,
                                               NegativeAcks = queue.Info.NegativeAcknowledge,
                                               Acks = queue.Info.Acknowledges,
                                               TimeoutMessages = queue.Info.TimedOutMessages,
                                               SavedMessages = queue.Info.MessageSaved,
                                               RemovedMessages = queue.Info.MessageRemoved,
                                               Errors = queue.Info.ErrorCount,
                                               LastMessageReceived = queue.Info.GetLastMessageReceiveUnix(),
                                               LastMessageSent = queue.Info.GetLastMessageSendUnix(),
                                               MessageLimit = queue.Options.MessageLimit,
                                               MessageSizeLimit = queue.Options.MessageSizeLimit
                                           };

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.QueueInformation;
            response.Serialize(information, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Gets connected instance list
        /// </summary>
        private async Task GetInstanceList(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanManageInstances(client, message);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<NodeInformation> list = new List<NodeInformation>();

            //slave instances
            List<MqClient> slaves = _server.NodeManager.Clients.GetAsClone();
            foreach (MqClient slave in slaves)
            {
                list.Add(new NodeInformation
                         {
                             IsSlave = true,
                             Host = slave.RemoteHost,
                             IsConnected = slave.IsConnected,
                             Id = slave.UniqueId,
                             Name = slave.Name,
                             Lifetime = slave.ConnectedDate.LifetimeMilliseconds()
                         });
            }

            //master instances
            foreach (TmqStickyConnector connector in _server.NodeManager.Connectors)
            {
                NodeOptions options = connector.Tag as NodeOptions;
                TmqClient c = connector.GetClient();

                list.Add(new NodeInformation
                         {
                             IsSlave = false,
                             Host = options?.Host,
                             IsConnected = connector.IsConnected,
                             Id = c.ClientId,
                             Name = options?.Name,
                             Lifetime = Convert.ToInt64(connector.Lifetime.TotalMilliseconds)
                         });
            }

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.InstanceList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Client

        /// <summary>
        /// Gets all connected clients
        /// </summary>
        public async Task GetClients(MqClient client, TmqMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveClients(client);
            if (!grant)
            {
                if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<ClientInformation> list = new List<ClientInformation>();

            string filter = null;
            if (!string.IsNullOrEmpty(message.Target))
                filter = message.Target;

            foreach (MqClient mc in _server.Clients)
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    if (string.IsNullOrEmpty(mc.Type) || !Filter.CheckMatch(mc.Type, filter))
                        continue;
                }

                list.Add(new ClientInformation
                         {
                             Id = mc.UniqueId,
                             Name = mc.Name,
                             Type = mc.Type,
                             IsAuthenticated = mc.IsAuthenticated,
                             Online = mc.ConnectedDate.LifetimeMilliseconds(),
                         });
            }

            TmqMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.ClientList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion
    }
}