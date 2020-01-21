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
using Twino.MQ.Security;
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
        private readonly MqServer _server;

        public ServerMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            try
            {
                await HandleUnsafe(client, message);
            }
            catch (OperationCanceledException)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.LimitExceeded));
            }
            catch (DuplicateNameException)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Duplicate));
            }
        }

        private async Task HandleUnsafe(MqClient client, TmqMessage message)
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
                    await CreateChannel(client, message, false);
                    break;

                //get channel information list
                case KnownContentTypes.ChannelList:
                    await GetChannelList(client, message);
                    break;

                //get channel information
                case KnownContentTypes.ChannelInformation:
                    await GetChannelInformation(client, message);
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

                //get queue information list
                case KnownContentTypes.QueueList:
                    await GetQueueList(client, message);
                    break;

                //get queue information
                case KnownContentTypes.QueueInformation:
                    await GetQueueInformation(client, message);
                    break;

                //get queue information
                case KnownContentTypes.InstanceList:
                    await GetInstanceList(client, message);
                    break;

                //for not-defines content types, use user-defined message handler
                default:
                    if (_server.ServerMessageHandler != null)
                        await _server.ServerMessageHandler.Received(client, message);
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

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            ChannelClient found = channel.FindClient(client.UniqueId);
            if (found != null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));

                return;
            }

            ClientJoinResult result = await channel.AddClient(client);
            if (message.ResponseRequired)
            {
                switch (result)
                {
                    case ClientJoinResult.Success:
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
                        break;

                    case ClientJoinResult.Unauthorized:
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                        break;

                    case ClientJoinResult.Full:
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.LimitExceeded));
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
                    if (message.ResponseRequired)
                        await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));

                    return null;
                }
            }

            if (!createForQueue && message.Length > 0 && message.Content != null && message.Content.Length > 0)
            {
                NetworkOptionsBuilder builder = new NetworkOptionsBuilder();
                builder.Load(message.ToString());

                ChannelOptions options = ChannelOptions.CloneFrom(_server.Options);
                builder.ApplyToChannel(options);

                IChannelEventHandler eventHandler = _server.DefaultChannelEventHandler;
                if (!string.IsNullOrEmpty(builder.ChannelEventHandler))
                {
                    IChannelEventHandler e = _server.Registry.GetChannelEvent(builder.ChannelEventHandler);
                    if (e != null)
                        eventHandler = e;
                }

                IChannelAuthenticator authenticator = _server.DefaultChannelAuthenticator;
                if (!string.IsNullOrEmpty(builder.ChannelAuthenticator))
                {
                    IChannelAuthenticator e = _server.Registry.GetChannelAuthenticator(builder.ChannelAuthenticator);
                    if (e != null)
                        authenticator = e;
                }

                IMessageDeliveryHandler deliveryHandler = _server.DefaultDeliveryHandler;
                if (!string.IsNullOrEmpty(builder.MessageDeliveryHandler))
                {
                    IMessageDeliveryHandler e = _server.Registry.GetMessageDelivery(builder.MessageDeliveryHandler);
                    if (e != null)
                        deliveryHandler = e;
                }

                Channel ch = _server.CreateChannel(message.Target, authenticator, eventHandler, deliveryHandler, options);

                if (ch != null && message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
            }

            Channel c = _server.CreateChannel(message.Target);

            if (!createForQueue && c != null && message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));

            return c;
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

        /// <summary>
        /// Finds the channel and sends the information
        /// </summary>
        private async Task GetChannelList(MqClient client, TmqMessage message)
        {
            List<ChannelInformation> list = new List<ChannelInformation>();
            foreach (Channel channel in _server.Channels)
            {
                if (channel == null)
                    continue;

                //authenticate for channel
                if (_server.DefaultChannelAuthenticator != null)
                {
                    bool grant = await _server.DefaultChannelAuthenticator.Authenticate(channel, client);
                    if (!grant)
                        continue;
                }

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
                             HideClientNames = channel.Options.HideClientNames
                         });
            }

            TmqMessage response = message.CreateResponse();
            message.ContentType = KnownContentTypes.ChannelList;
            await response.SetJsonContent(list);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Finds the channel and sends the information
        /// </summary>
        private async Task GetChannelInformation(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //authenticate for channel
            if (_server.DefaultChannelAuthenticator != null)
            {
                bool grant = await _server.DefaultChannelAuthenticator.Authenticate(channel, client);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
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
                                                 HideClientNames = channel.Options.HideClientNames
                                             };

            TmqMessage response = message.CreateResponse();
            message.ContentType = KnownContentTypes.ChannelInformation;
            await response.SetJsonContent(information);
            await client.SendAsync(response);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task CreateQueue(MqClient client, TmqMessage message)
        {
            ushort? contentType;
            NetworkOptionsBuilder builder = null;
            if (message.Length == 2)
            {
                byte[] bytes = new byte[2];
                await message.Content.ReadAsync(bytes);
                contentType = BitConverter.ToUInt16(bytes);
            }
            else
            {
                builder = new NetworkOptionsBuilder();
                builder.Load(message.ToString());
                contentType = builder.Id;
            }

            Channel channel = await CreateChannel(client, message, true);
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
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            IMessageDeliveryHandler delivery = channel.DeliveryHandler;
            if (builder != null)
            {
                builder.ApplyToQueue(options);
                if (!string.IsNullOrEmpty(builder.MessageDeliveryHandler))
                {
                    IMessageDeliveryHandler found = _server.Registry.GetMessageDelivery(builder.MessageDeliveryHandler);
                    if (found != null)
                        delivery = found;
                }
            }

            queue = await channel.CreateQueue(contentType.Value, options, delivery);

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

            if (message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task UpdateQueue(MqClient client, TmqMessage message)
        {
            NetworkOptionsBuilder builder = new NetworkOptionsBuilder();
            builder.Load(message.ToString());

            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                if (message.ResponseRequired)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));

                return;
            }

            ChannelQueue queue = channel.FindQueue(builder.Id);
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

            builder.ApplyToQueue(queue.Options);
            if (builder.Status.HasValue)
                await queue.SetStatus(builder.Status.Value);

            //if creation successful, sends response
            if (message.ResponseRequired)
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Ok));
        }

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        private async Task GetQueueList(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //authenticate for channel
            if (_server.DefaultChannelAuthenticator != null)
            {
                bool grant = await _server.DefaultChannelAuthenticator.Authenticate(channel, client);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            List<QueueInformation> list = new List<QueueInformation>();
            foreach (ChannelQueue queue in channel.QueuesClone)
            {
                if (queue == null)
                    continue;

                list.Add(new QueueInformation
                         {
                             Channel = channel.Name,
                             Id = queue.Id,
                             Status = queue.Status.ToString().ToLower(),
                             InQueueHighPriorityMessages = queue.HighPriorityLinkedList.Count,
                             InQueueRegularMessages = queue.RegularLinkedList.Count,
                             OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                             RequestAcknowledge = channel.Options.RequestAcknowledge,
                             AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                             MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                             WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                             HideClientNames = channel.Options.HideClientNames,
                             ReceivedMessages = queue.Info.ReceivedMessages,
                             SentMessages = queue.Info.SentMessages,
                             Deliveries = queue.Info.Deliveries,
                             Unacknowledges = queue.Info.Unacknowledges,
                             Acknowledges = queue.Info.Acknowledges,
                             TimeoutMessages = queue.Info.TimedOutMessages,
                             SavedMessages = queue.Info.MessageSaved,
                             RemovedMessages = queue.Info.MessageRemoved,
                             Errors = queue.Info.ErrorCount,
                             LastMessageReceived = queue.Info.GetLastMessageReceiveUnix(),
                             LastMessageSent = queue.Info.GetLastMessageSendUnix()
                         });
            }

            TmqMessage response = message.CreateResponse();
            message.ContentType = KnownContentTypes.QueueList;
            await response.SetJsonContent(list);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Finds the queue and sends the information
        /// </summary>
        private async Task GetQueueInformation(MqClient client, TmqMessage message)
        {
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //authenticate for channel
            if (_server.DefaultChannelAuthenticator != null)
            {
                bool grant = await _server.DefaultChannelAuthenticator.Authenticate(channel, client);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            byte[] bytes = new byte[2];
            await message.Content.ReadAsync(bytes);
            ushort id = BitConverter.ToUInt16(bytes);
            ChannelQueue queue = channel.FindQueue(id);
            if (queue == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            QueueInformation information = new QueueInformation
                                           {
                                               Channel = channel.Name,
                                               Id = id,
                                               Status = queue.Status.ToString().ToLower(),
                                               InQueueHighPriorityMessages = queue.HighPriorityLinkedList.Count,
                                               InQueueRegularMessages = queue.RegularLinkedList.Count,
                                               OnlyFirstAcquirer = channel.Options.SendOnlyFirstAcquirer,
                                               RequestAcknowledge = channel.Options.RequestAcknowledge,
                                               AcknowledgeTimeout = Convert.ToInt32(channel.Options.AcknowledgeTimeout.TotalMilliseconds),
                                               MessageTimeout = Convert.ToInt32(channel.Options.MessageTimeout.TotalMilliseconds),
                                               WaitForAcknowledge = channel.Options.WaitForAcknowledge,
                                               HideClientNames = channel.Options.HideClientNames,
                                               ReceivedMessages = queue.Info.ReceivedMessages,
                                               SentMessages = queue.Info.SentMessages,
                                               Deliveries = queue.Info.Deliveries,
                                               Unacknowledges = queue.Info.Unacknowledges,
                                               Acknowledges = queue.Info.Acknowledges,
                                               TimeoutMessages = queue.Info.TimedOutMessages,
                                               SavedMessages = queue.Info.MessageSaved,
                                               RemovedMessages = queue.Info.MessageRemoved,
                                               Errors = queue.Info.ErrorCount,
                                               LastMessageReceived = queue.Info.GetLastMessageReceiveUnix(),
                                               LastMessageSent = queue.Info.GetLastMessageSendUnix()
                                           };

            TmqMessage response = message.CreateResponse();
            message.ContentType = KnownContentTypes.QueueInformation;
            await response.SetJsonContent(information);
            await client.SendAsync(response);
        }

        #endregion

        /// <summary>
        /// Gets connected instance list
        /// </summary>
        private async Task GetInstanceList(MqClient client, TmqMessage message)
        {
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanManageInstances(client, message);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            List<InstanceInformation> list = new List<InstanceInformation>();

            //slave instances
            List<SlaveInstance> slaves = _server.SlaveInstances.GetAsClone();
            foreach (SlaveInstance slave in slaves)
            {
                list.Add(new InstanceInformation
                         {
                             IsSlave = true,
                             Host = slave.RemoteHost,
                             IsConnected = slave.Client.IsConnected,
                             Id = slave.Client.UniqueId,
                             Name = slave.Client.Name,
                             Lifetime = slave.ConnectedDate.LifetimeMilliseconds()
                         });
            }

            //master instances
            foreach (TmqStickyConnector connector in _server.InstanceConnectors)
            {
                InstanceOptions options = connector.Tag as InstanceOptions;
                TmqClient c = connector.GetClient();

                list.Add(new InstanceInformation
                         {
                             IsSlave = false,
                             Host = options?.Host,
                             IsConnected = connector.IsConnected,
                             Id = c.ClientId,
                             Name = options?.Name,
                             Lifetime = Convert.ToInt64(connector.Lifetime.TotalMilliseconds)
                         });
            }

            TmqMessage response = message.CreateResponse();
            message.ContentType = KnownContentTypes.InstanceList;
            await response.SetJsonContent(list);
            await client.SendAsync(response);
        }
    }
}