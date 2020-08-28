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
using Twino.MQ.Routing;
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

        public Task Handle(MqClient client, TwinoMessage message, bool fromNode)
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

        private Task HandleUnsafe(MqClient client, TwinoMessage message)
        {
            switch (message.ContentType)
            {
                //subscribe to a queue
                case KnownContentTypes.Subscribe:
                    return Subscribe(client, message);

                //unsubscribe from a queue
                case KnownContentTypes.Unsubscribe:
                    return Unsubscribe(client, message);

                //get channel consumers
                case KnownContentTypes.QueueConsumers:
                    return GetQueueConsumers(client, message);

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

                //lists all routers
                case KnownContentTypes.ListRouters:
                    return ListRouters(client, message);

                //creates new router
                case KnownContentTypes.CreateRouter:
                    return CreateRouter(client, message);

                //removes a router
                case KnownContentTypes.RemoveRouter:
                    return RemoveRouter(client, message);

                //lists all bindings of a router
                case KnownContentTypes.ListBindings:
                    return ListRouterBindings(client, message);

                //adds new binding to a router
                case KnownContentTypes.AddBinding:
                    return CreateRouterBinding(client, message);

                //removes a binding from a router
                case KnownContentTypes.RemoveBinding:
                    return RemoveRouterBinding(client, message);

                //for not-defines content types, use user-defined message handler
                default:
                    if (_server.ServerMessageHandler != null)
                        return _server.ServerMessageHandler.Received(client, message);

                    return Task.CompletedTask;
            }
        }

        #endregion

        #region Queue

        /// <summary>
        /// Finds and joins to channel and sends response
        /// </summary>
        private async Task Subscribe(MqClient client, TwinoMessage message)
        {
            TwinoQueue queue = _server.FindQueue(message.Target);

            //if auto creation active, try to create channel
            if (queue == null && _server.Options.AutoQueueCreation)
            {
                QueueOptions options = QueueOptions.CloneFrom(_server.Options);
                queue = await _server.CreateQueue(message.Target, options, message, _server.DeliveryHandlerFactory);
            }

            if (queue == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            QueueClient found = queue.FindClient(client.UniqueId);
            if (found != null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));

                return;
            }

            QueueSubscriptionResult result = await queue.AddClient(client);
            if (message.WaitResponse)
            {
                switch (result)
                {
                    case QueueSubscriptionResult.Success:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
                        break;

                    case QueueSubscriptionResult.Unauthorized:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                        break;

                    case QueueSubscriptionResult.Full:
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.LimitExceeded));
                        break;
                }
            }
        }

        /// <summary>
        /// Leaves from the channel and sends response
        /// </summary>
        private async Task Unsubscribe(MqClient client, TwinoMessage message)
        {
            TwinoQueue queue = _server.FindQueue(message.Target);
            if (queue == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            bool success = await queue.RemoveClient(client);

            if (message.WaitResponse)
                await client.SendAsync(message.CreateResponse(success ? TwinoResultCode.Ok : TwinoResultCode.NotFound));
        }

        /// <summary>
        /// Gets active consumers of channel 
        /// </summary>
        public async Task GetQueueConsumers(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            TwinoQueue queue = _server.FindQueue(message.Target);

            if (queue == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveQueueConsumers(client, queue);
            if (!grant)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<ClientInformation> list = new List<ClientInformation>();

            foreach (QueueClient cc in queue.ClientsClone)
                list.Add(new ClientInformation
                         {
                             Id = cc.Client.UniqueId,
                             Name = cc.Client.Name,
                             Type = cc.Client.Type,
                             IsAuthenticated = cc.Client.IsAuthenticated,
                             Online = cc.JoinDate.LifetimeMilliseconds(),
                         });

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.QueueConsumers;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task CreateQueue(MqClient client, TwinoMessage message)
        {
            NetworkOptionsBuilder builder = null;
            if (message.Length > 0)
                builder = await System.Text.Json.JsonSerializer.DeserializeAsync<NetworkOptionsBuilder>(message.Content);

            TwinoQueue queue = _server.FindQueue(message.Target);

            //if queue exists, we can't create. return duplicate response.
            if (queue != null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Duplicate));

                return;
            }

            //check authority if client can create queue
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateQueue(client, message.Target, builder);
                if (!grant)
                {
                    if (message.WaitResponse)
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                    return;
                }
            }

            //creates new queue
            QueueOptions options = QueueOptions.CloneFrom(_server.Options);
            queue = await _server.CreateQueue(message.Target, options, message, _server.DeliveryHandlerFactory);

            //if creation successful, sends response
            if (message.WaitResponse)
            {
                if (queue != null)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
                else
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Failed));
            }
        }

        /// <summary>
        /// Removes a queue from a channel
        /// </summary>
        private async Task RemoveQueue(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            TwinoQueue queue = _server.FindQueue(message.Target);
            if (queue == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            bool grant = await _server.AdminAuthorization.CanRemoveQueue(client, queue);
            if (!grant)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            await _server.RemoveQueue(queue);

            if (message.WaitResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Creates new queue and sends response
        /// </summary>
        private async Task UpdateQueue(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            NetworkOptionsBuilder builder = await System.Text.Json.JsonSerializer.DeserializeAsync<NetworkOptionsBuilder>(message.Content);

            TwinoQueue queue = _server.FindQueue(message.Target);
            if (queue == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanUpdateQueueOptions(client, queue, builder);
            if (!grant)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            builder.ApplyToQueue(queue.Options);
            if (builder.Status.HasValue)
                await queue.SetStatus(builder.Status.Value);

            _server.OnQueueUpdated.Trigger(queue);

            //if creation successful, sends response
            if (message.WaitResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Clears messages in a queue
        /// </summary>
        private async Task ClearMessages(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            TwinoQueue queue = _server.FindQueue(message.Target);
            if (queue == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            string prio = message.FindHeader(TwinoHeaders.PRIORITY_MESSAGES);
            string msgs = message.FindHeader(TwinoHeaders.MESSAGES);
            bool clearPrio = !string.IsNullOrEmpty(prio) && prio.Equals("yes", StringComparison.InvariantCultureIgnoreCase);
            bool clearMsgs = !string.IsNullOrEmpty(msgs) && msgs.Equals("yes", StringComparison.InvariantCultureIgnoreCase);

            bool grant = await _server.AdminAuthorization.CanClearQueueMessages(client, queue, clearPrio, clearMsgs);
            if (!grant)
            {
                if (message.WaitResponse)
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
            if (message.WaitResponse)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        private async Task GetQueueList(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            List<QueueInformation> list = new List<QueueInformation>();
            foreach (TwinoQueue queue in _server.Queues)
            {
                if (queue == null)
                    continue;

                string ack = "none";
                if (queue.Options.Acknowledge == QueueAckDecision.JustRequest)
                    ack = "just";
                else if (queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                    ack = "wait";

                list.Add(new QueueInformation
                         {
                             Name = queue.Name,
                             Topic = queue.Topic,
                             Status = queue.Status.ToString().Trim().ToLower(),
                             PriorityMessages = queue.PriorityMessagesList.Count,
                             Messages = queue.MessagesList.Count,
                             Acknowledge = ack,
                             AcknowledgeTimeout = Convert.ToInt32(queue.Options.AcknowledgeTimeout.TotalMilliseconds),
                             MessageTimeout = Convert.ToInt32(queue.Options.MessageTimeout.TotalMilliseconds),
                             HideClientNames = queue.Options.HideClientNames,
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

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.QueueList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        /// <summary>
        /// Finds the queue and sends the information
        /// </summary>
        private async Task GetQueueInformation(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            TwinoQueue queue = _server.FindQueue(message.Target);
            bool grant = await _server.AdminAuthorization.CanReceiveQueueInfo(client, queue);
            if (!grant)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            if (queue == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            string ack = "none";
            if (queue.Options.Acknowledge == QueueAckDecision.JustRequest)
                ack = "just";
            else if (queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                ack = "wait";

            QueueInformation information = new QueueInformation
                                           {
                                               Name = queue.Name,
                                               Topic = queue.Topic,
                                               Status = queue.Status.ToString().Trim().ToLower(),
                                               PriorityMessages = queue.PriorityMessagesList.Count,
                                               Acknowledge = ack,
                                               Messages = queue.MessagesList.Count,
                                               AcknowledgeTimeout = Convert.ToInt32(queue.Options.AcknowledgeTimeout.TotalMilliseconds),
                                               MessageTimeout = Convert.ToInt32(queue.Options.MessageTimeout.TotalMilliseconds),
                                               HideClientNames = queue.Options.HideClientNames,
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

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.QueueInformation;
            response.Serialize(information, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Gets connected instance list
        /// </summary>
        private async Task GetInstanceList(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanManageInstances(client, message);
            if (!grant)
            {
                if (message.WaitResponse)
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

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.InstanceList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Client

        /// <summary>
        /// Gets all connected clients
        /// </summary>
        public async Task GetClients(MqClient client, TwinoMessage message)
        {
            if (_server.AdminAuthorization == null)
            {
                if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));

                return;
            }

            bool grant = await _server.AdminAuthorization.CanReceiveClients(client);
            if (!grant)
            {
                if (message.WaitResponse)
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

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            message.ContentType = KnownContentTypes.ClientList;
            response.Serialize(list, _server.MessageContentSerializer);
            await client.SendAsync(response);
        }

        #endregion

        #region Router

        /// <summary>
        /// Creates new router
        /// </summary>
        private async Task CreateRouter(MqClient client, TwinoMessage message)
        {
            IRouter found = _server.FindRouter(message.Target);
            if (found != null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
                return;
            }

            string methodHeader = message.FindHeader(TwinoHeaders.ROUTE_METHOD);
            RouteMethod method = RouteMethod.Distribute;
            if (!string.IsNullOrEmpty(methodHeader))
                method = (RouteMethod) Convert.ToInt32(methodHeader);

            //check create channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateRouter(client, message.Target, method);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            _server.AddRouter(message.Target, method);
            await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Removes a router with it's bindings
        /// </summary>
        private async Task RemoveRouter(MqClient client, TwinoMessage message)
        {
            IRouter found = _server.FindRouter(message.Target);
            if (found == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
                return;
            }

            //check create channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanRemoveRouter(client, found);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            _server.RemoveRouter(found);
            await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Sends all routers
        /// </summary>
        private async Task ListRouters(MqClient client, TwinoMessage message)
        {
            List<RouterInformation> items = new List<RouterInformation>();
            foreach (IRouter router in _server.Routers)
            {
                RouterInformation info = new RouterInformation
                                         {
                                             Name = router.Name,
                                             IsEnabled = router.IsEnabled
                                         };

                if (router is Router r)
                    info.Method = r.Method;

                items.Add(info);
            }

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            response.Serialize(items, new NewtonsoftContentSerializer());
            await client.SendAsync(response);
        }

        /// <summary>
        /// Creates new binding for a router
        /// </summary>
        private async Task CreateRouterBinding(MqClient client, TwinoMessage message)
        {
            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            BindingInformation info = message.Deserialize<BindingInformation>(new NewtonsoftContentSerializer());

            //check create channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanCreateBinding(client, router, info);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            switch (info.BindingType)
            {
                case BindingType.Direct:
                    router.AddBinding(new DirectBinding(info.Name, info.Target, info.ContentType, info.Priority, info.Interaction, info.Method));
                    break;

                case BindingType.Queue:
                    router.AddBinding(new QueueBinding(info.Name, info.Target, info.ContentType ?? 0, info.Priority, info.Interaction));
                    break;

                case BindingType.Http:
                    router.AddBinding(new HttpBinding(info.Name, info.Target, (HttpBindingMethod) (info.ContentType ?? 0), info.Priority, info.Interaction));
                    break;

                case BindingType.Topic:
                    router.AddBinding(new TopicBinding(info.Name, info.Target, info.ContentType ?? 0, info.Priority, info.Interaction, info.Method));
                    break;
            }

            await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Removes a router with it's bindings
        /// </summary>
        private async Task RemoveRouterBinding(MqClient client, TwinoMessage message)
        {
            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            string name = message.FindHeader(TwinoHeaders.BINDING_NAME);
            if (string.IsNullOrEmpty(name))
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            Binding[] bindings = router.GetBindings();
            Binding binding = bindings.FirstOrDefault(x => x.Name == name);
            if (binding == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            //check create channel access
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanRemoveBinding(client, binding);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            router.RemoveBinding(binding);
            await client.SendAsync(message.CreateResponse(TwinoResultCode.Ok));
        }

        /// <summary>
        /// Sends all bindings of a router
        /// </summary>
        private async Task ListRouterBindings(MqClient client, TwinoMessage message)
        {
            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            List<BindingInformation> items = new List<BindingInformation>();
            foreach (Binding binding in router.GetBindings())
            {
                BindingInformation info = new BindingInformation
                                          {
                                              Name = binding.Name,
                                              Target = binding.Target,
                                              Priority = binding.Priority,
                                              ContentType = binding.ContentType,
                                              Interaction = binding.Interaction
                                          };

                if (binding is QueueBinding)
                    info.BindingType = BindingType.Queue;
                else if (binding is DirectBinding directBinding)
                {
                    info.Method = directBinding.RouteMethod;
                    info.BindingType = BindingType.Direct;
                }
                else if (binding is TopicBinding topicBinding)
                {
                    info.Method = topicBinding.RouteMethod;
                    info.BindingType = BindingType.Topic;
                }
                else if (binding is HttpBinding)
                    info.BindingType = BindingType.Http;

                items.Add(info);
            }

            TwinoMessage response = message.CreateResponse(TwinoResultCode.Ok);
            response.Serialize(items, new NewtonsoftContentSerializer());
            await client.SendAsync(response);
        }

        #endregion
    }
}