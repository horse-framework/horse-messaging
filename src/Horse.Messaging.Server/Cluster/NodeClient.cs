using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EnumsNET;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Describes a remote node
    /// </summary>
    public class NodeClient
    {
        #region Fields - Properties

        private readonly HorseClient _outgoingClient;
        private readonly NodeDeliveryTracker _deliveryTracker;
        private MessagingClient _incomingClient;
        private bool _connectedToRemote;
        public event Action<NodeClient> OnDisconnected;

        /// <summary>
        /// Node Info
        /// </summary>
        public NodeInfo Info { get; }

        /// <summary>
        /// Rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// When mainity is asked, the answer of the remote node.
        /// True, if approved.
        /// </summary>
        internal bool ApprovedMainity { get; set; }

        /// <summary>
        /// Node connected date
        /// </summary>
        public DateTime ConnectedDate { get; private set; }

        /// <summary>
        /// Returns true if there is active connection to the node
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_outgoingClient != null && _outgoingClient.IsConnected)
                    return true;

                if (_incomingClient != null && _incomingClient.IsConnected)
                    return true;

                return false;
            }
        }

        #endregion

        /// <summary>
        /// Creates new node client
        /// </summary>
        public NodeClient(HorseRider rider, NodeInfo info)
        {
            Rider = rider;
            Info = info;

            _outgoingClient = new HorseClient();
            _outgoingClient.ReconnectWait = TimeSpan.FromMilliseconds(500);
            _outgoingClient.AddHost(info.Host);

            _outgoingClient.SetClientName(Rider.Cluster.Options.Name);
            _outgoingClient.SetClientType("Node");
            _outgoingClient.SetClientToken(rider.Cluster.Options.SharedSecret);

            _outgoingClient.AddProperty(HorseHeaders.HORSE_NODE, HorseHeaders.YES);
            _outgoingClient.AddProperty(HorseHeaders.NODE_ID, rider.Cluster.Id);
            _outgoingClient.AddProperty(HorseHeaders.NODE_HOST, rider.Cluster.Options.NodeHost);
            _outgoingClient.AddProperty(HorseHeaders.NODE_PUBLIC_HOST, rider.Cluster.Options.PublicHost);
            _outgoingClient.AddProperty(HorseHeaders.NODE_START, Rider.Cluster.StartDate.ToUnixMilliseconds().ToString());

            _outgoingClient.MessageReceived += ProcessReceivedMessage;
            _outgoingClient.Disconnected += OutgoingClientOnDisconnected;
            _outgoingClient.Connected += OutgoingClientOnConnected;

            _deliveryTracker = new NodeDeliveryTracker(this);
            _deliveryTracker.Run();
        }

        #region Connections

        internal void Start()
        {
            _ = _outgoingClient.ConnectAsync();
        }

        private void OutgoingClientOnConnected(HorseClient client)
        {
            if (_incomingClient == null || !_incomingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;

            _connectedToRemote = true;
            Rider.Server.Logger?.LogEvent("CLUSTER", $"Connected to remote client: {Info.Name}");
        }

        internal void IncomingClientConnected(MessagingClient incomingClient, ConnectionData data)
        {
            _incomingClient = incomingClient;
            incomingClient.NodeClient = this;
            incomingClient.IsNodeClient = true;
            Info.Name = incomingClient.Name;

            Info.Id = data.Properties.GetStringValue(HorseHeaders.NODE_ID);
            Info.PublicHost = data.Properties.GetStringValue(HorseHeaders.NODE_PUBLIC_HOST);
            Info.StartDate = Convert.ToInt64(data.Properties.GetStringValue(HorseHeaders.NODE_START).Trim()).ToUnixDate();

            if (_outgoingClient == null || !_outgoingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;

            incomingClient.Disconnected += IncomingClientOnDisconnected;

            HorseMessage infoMessage = new HorseMessage(MessageType.Cluster, Rider.Cluster.Options.Name, KnownContentTypes.NodeInformation);
            infoMessage.AddHeader(HorseHeaders.CLIENT_NAME, Rider.Cluster.Options.Name);
            infoMessage.AddHeader(HorseHeaders.NODE_ID, Rider.Cluster.Id);
            infoMessage.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, Rider.Cluster.Options.PublicHost);
            infoMessage.AddHeader(HorseHeaders.NODE_START, Rider.Cluster.StartDate.ToUnixMilliseconds().ToString());

            incomingClient.Send(infoMessage);

            if (Rider.Cluster.Options.Mode == ClusterMode.Reliable)
            {
                if (Rider.Cluster.State == NodeState.Main)
                    _ = Rider.Cluster.AnnounceMainity();

                else if (Rider.Cluster.State == NodeState.Single)
                {
                    HorseMessage whoIsMain = new HorseMessage(MessageType.Cluster, Rider.Cluster.Options.Name, KnownContentTypes.WhoIsMainNode);
                    incomingClient.Send(whoIsMain);

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(new Random().Next(250, 1000));
                            if (Rider.Cluster.MainNode == null)
                                _ = Rider.Cluster.AskForMain();
                        }
                        catch
                        {
                        }
                    });
                }
            }

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Remote client is connected: {Info.Name}");
        }

        private void OutgoingClientOnDisconnected(HorseClient client)
        {
            if (_incomingClient == null || !_incomingClient.IsConnected)
                _ = ProcessDisconnection();

            if (_connectedToRemote)
                Rider.Server.Logger?.LogEvent("CLUSTER", $"Disconnected from remote client: {Info.Name}");

            _connectedToRemote = false;
        }

        private void IncomingClientOnDisconnected(SocketBase client)
        {
            if (_outgoingClient == null || !_outgoingClient.IsConnected)
                _ = ProcessDisconnection();

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Remote client is disconnected: {Info.Name}");
        }

        private async Task ProcessDisconnection()
        {
            if (IsConnected)
                return;

            Info.StartDate = null;
            ClusterManager cluster = Rider.Cluster;

            OnDisconnected?.Invoke(this);

            if (cluster.Options.Mode == ClusterMode.Scaled)
                return;

            if (cluster.MainNode != null && cluster.MainNode.Id == Info?.Id)
                await cluster.OnMainDown(this);

            else if (cluster.SuccessorNode != null && cluster.SuccessorNode.Id == Info?.Id)
                await cluster.OnSuccessorDown(this);

            cluster.UpdateState();
        }

        #endregion

        #region Receive and Process

        internal void ProcessReceivedMessage(HorseClient client, HorseMessage message)
        {
            if (message.Type == MessageType.Cluster)
                ProcessClusterMessage(message, false);
            else
                ProcessScaledMessage(message);
        }

        internal void ProcessReceivedMessage(MessagingClient client, HorseMessage message)
        {
            if (message.Type == MessageType.Cluster)
                ProcessClusterMessage(message, true);
            else
                ProcessScaledMessage(message);
        }

        private void ProcessScaledMessage(HorseMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Channel:
                case MessageType.Cache:
                case MessageType.DirectMessage:
                    break;

                case MessageType.Response:
                    _deliveryTracker.Commit(message.MessageId);
                    break;
            }
        }

        private void ProcessClusterMessage(HorseMessage message, bool fromIncomingClient)
        {
            ClusterManager cluster = Rider.Cluster;
            switch (message.ContentType)
            {
                #region Node Management

                case KnownContentTypes.NodeInformation:
                {
                    if (fromIncomingClient)
                        return;

                    Info.Id = message.FindHeader(HorseHeaders.NODE_ID);
                    Info.PublicHost = message.FindHeader(HorseHeaders.NODE_PUBLIC_HOST);
                    Info.Name = message.FindHeader(HorseHeaders.CLIENT_NAME);
                    break;
                }

                case KnownContentTypes.WhoIsMainNode:
                {
                    if (cluster.Options.Mode == ClusterMode.Reliable && cluster.State == NodeState.Main)
                        _ = cluster.AnnounceMainity();

                    break;
                }

                case KnownContentTypes.MainNodeAnnouncement:
                {
                    MainNodeAnnouncement msg = JsonSerializer.Deserialize<MainNodeAnnouncement>(message.GetStringContent(), SerializerFactory.Default());
                    cluster.OnMainAnnounced(this, msg);
                    break;
                }

                case KnownContentTypes.MainAnnouncementAnswer:
                    bool approved = message.GetStringContent().Trim() == "1";
                    _ = cluster.OnRequestAnswered(this, approved);
                    break;

                case KnownContentTypes.ProdForMainAnnouncement:
                    _ = cluster.OnProdForAnnouncement();
                    break;

                case KnownContentTypes.AskForMainPermission:
                    _ = cluster.OnMainRequested(this);
                    break;

                #endregion

                #region Queue Sync

                case KnownContentTypes.NodeTriggerQueueListRequest:
                {
                    HorseMessage msg = new HorseMessage(MessageType.Cluster, message.Source, KnownContentTypes.NodeQueueListRequest);
                    _ = SendMessage(msg);
                    break;
                }

                case KnownContentTypes.NodeQueueListRequest:
                    if (cluster.State == NodeState.Main)
                    {
                        List<NodeQueueInfo> infoList = Rider.Queue.Queues.Select(x => x.ClusterNotifier.CreateNodeQueueInfo()).ToList();
                        HorseMessage listMessage = new HorseMessage(MessageType.Cluster, Info.Id, KnownContentTypes.NodeQueueListResponse);
                        listMessage.SetStringContent(JsonSerializer.Serialize(infoList, infoList.GetType(), SerializerFactory.Default()));
                        _ = SendMessage(listMessage);
                    }

                    break;

                case KnownContentTypes.NodeQueueListResponse:
                {
                    List<NodeQueueInfo> infoList = JsonSerializer.Deserialize<List<NodeQueueInfo>>(message.GetStringContent(), SerializerFactory.Default());
                    _ = cluster.ProcessQueueList(this, infoList);
                    break;
                }

                case KnownContentTypes.NodeQueueSyncRequest:
                {
                    if (cluster.State == NodeState.Main)
                    {
                        HorseQueue queue = Rider.Queue.Find(message.Target);

                        if (queue != null)
                            _ = queue.Manager.Synchronizer.BeginSharing(this);
                        else
                            _ = SendMessage(new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.RemoveQueue));
                    }

                    break;
                }

                case KnownContentTypes.NodeQueueMessageIdList:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    if (queue != null)
                        _ = queue.Manager.Synchronizer.ProcessMessageList(message);

                    break;
                }

                case KnownContentTypes.NodeQueueMessageRequest:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    if (queue != null)
                        _ = queue.Manager.Synchronizer.SendMessages(message);

                    break;
                }

                case KnownContentTypes.NodeQueueMessageResponse:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    if (queue != null)
                        _ = ProcessSync(message, queue);

                    break;
                }

                case KnownContentTypes.NodeQueueSyncReverseMessages:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    if (queue != null)
                        _ = queue.Manager.Synchronizer.ProcessReceivedMessages(message, false);

                    break;
                }

                case KnownContentTypes.NodeQueueSyncCompletion:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    if (queue != null)
                        _ = queue.Manager.Synchronizer.EndSharing();

                    break;
                }

                #endregion

                #region Queue Operations

                case KnownContentTypes.CreateQueue:
                {
                    string content = message.GetStringContent();
                    NodeQueueInfo queueInfo = JsonSerializer.Deserialize<NodeQueueInfo>(content, SerializerFactory.Default());
                    _ = Rider.Queue.CreateReplica(queueInfo);
                    break;
                }

                case KnownContentTypes.UpdateQueue:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);

                    if (queue != null)
                    {
                        string content = message.GetStringContent();
                        NodeQueueInfo queueInfo = JsonSerializer.Deserialize<NodeQueueInfo>(content, SerializerFactory.Default());
                        queue.UpdateOptionsByNodeInfo(queueInfo);
                    }

                    break;
                }

                case KnownContentTypes.RemoveQueue:
                    _ = Rider.Queue.Remove(message.Target);
                    break;

                case KnownContentTypes.NodePushQueueMessage:
                    _ = PushByNode(message);
                    break;

                case KnownContentTypes.NodePutBackQueueMessage:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);

                    if (queue != null)
                    {
                        QueueMessage found;
                        if (message.HighPriority)
                        {
                            found = queue.Manager.MessageStore.Find(message.MessageId)
                                    ?? queue.Manager.PriorityMessageStore.Find(message.MessageId);
                        }
                        else
                        {
                            found = queue.Manager.PriorityMessageStore.Find(message.MessageId)
                                    ?? queue.Manager.MessageStore.Find(message.MessageId);
                        }

                        if (found != null)
                            _ = queue.Manager.ChangeMessagePriority(found, message.HighPriority);
                    }

                    break;
                }

                case KnownContentTypes.NodeRemoveQueueMessage:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);

                    if (queue != null)
                        _ = queue.RemoveMessage(message.MessageId);

                    break;
                }

                case KnownContentTypes.NodeClearQueueMessage:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    queue?.ClearMessages(false);
                    break;
                }

                case KnownContentTypes.NodeQueueStateMessage:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);
                    queue?.SetStatus(Enums.Parse<QueueStatus>(message.GetStringContent()));
                    break;
                }

                #endregion

                #region Routers

                case KnownContentTypes.CreateRouter:
                {
                    string content = message.GetStringContent();
                    RouterConfiguration configuration = JsonSerializer.Deserialize<RouterConfiguration>(content, SerializerFactory.Default());
                    Router router = Rider.Router.CreateRouter(configuration);
                    Rider.Router.Add(router, false);
                    break;
                }

                case KnownContentTypes.RemoveRouter:
                {
                    Router router = Rider.Router.Find(message.Target);
                    if (router != null)
                        Rider.Router.Remove(router, false);
                    break;
                }

                case KnownContentTypes.AddBinding:
                {
                    string content = message.GetStringContent();
                    BindingConfiguration configuration = JsonSerializer.Deserialize<BindingConfiguration>(content, SerializerFactory.Default());
                    Router router = Rider.Router.Find(message.Target);

                    if (router != null)
                    {
                        Binding binding = Rider.Router.CreateBinding(configuration);
                        router.AddBinding(binding, false);
                    }

                    break;
                }

                case KnownContentTypes.RemoveBinding:
                {
                    string bindingName = message.GetStringContent();
                    Router router = Rider.Router.Find(message.Target);
                    router?.RemoveBinding(bindingName, false);
                    break;
                }

                #endregion

                #region Channels

                case KnownContentTypes.ChannelCreate:
                {
                    string content = message.GetStringContent();
                    HorseChannelOptions options = JsonSerializer.Deserialize<HorseChannelOptions>(content, SerializerFactory.Default());
                    _ = Rider.Channel.Create(message.Target, options, null, true, true, false);
                    break;
                }

                case KnownContentTypes.ChannelRemove:
                {
                    HorseChannel channel = Rider.Channel.Find(message.Target);
                    if (channel != null)
                        Rider.Channel.Remove(channel, false);
                    break;
                }

                case KnownContentTypes.ChannelUpdate:
                {
                    HorseChannel channel = Rider.Channel.Find(message.Target);
                    if (channel != null)
                    {
                        string content = message.GetStringContent();
                        HorseChannelOptions options = JsonSerializer.Deserialize<HorseChannelOptions>(content, SerializerFactory.Default());
                        channel.Options.ApplyFrom(options);
                        Rider.Channel.ApplyChangedOptions(channel);
                    }

                    break;
                }

                #endregion
            }
        }

        private async Task ProcessSync(HorseMessage message, HorseQueue queue)
        {
            await queue.Manager.Synchronizer.ProcessReceivedMessages(message, true);
            await queue.Manager.Synchronizer.EndReceiving(true);
        }

        #endregion

        #region Send

        internal async Task<bool> SendMessage(HorseMessage message)
        {
            if (_outgoingClient != null && _outgoingClient.IsConnected)
            {
                HorseResult result = await _outgoingClient.SendAsync(message);
                return result.Code == HorseResultCode.Ok;
            }

            if (_incomingClient != null && _incomingClient.IsConnected)
                return await _incomingClient.SendAsync(message);

            return false;
        }

        internal async Task<bool> SendMessageAndWaitAck(HorseMessage message)
        {
            TaskCompletionSource<NodeMessageDelivery> source = _deliveryTracker.Track(message);

            bool sent = await SendMessage(message);
            if (!sent)
            {
                _deliveryTracker.Untrack(message.MessageId);
                return false;
            }

            NodeMessageDelivery delivery = await source.Task;
            return delivery.IsCommitted;
        }

        private async Task PushByNode(HorseMessage message)
        {
            if (Rider.Cluster.MainNode == null || Rider.Cluster.MainNode.Id != Info?.Id)
            {
                await SendMessage(message.CreateAcknowledge(HorseHeaders.UNACCEPTABLE));
                return;
            }

            HorseQueue queue = Rider.Queue.Find(message.Target);
            if (queue == null)
            {
                QueueOptions options = QueueOptions.CloneFrom(Rider.Queue.Options);
                queue = await Rider.Queue.Create(message.Target, options, message, true, true);
            }

            PushResult result = PushResult.Empty;
            if (queue != null)
            {
                message.Type = MessageType.QueueMessage;
                message.ContentType = 0;

                result = await queue.PushByNode(message);
            }

            HorseMessage ack = message.CreateAcknowledge(result == PushResult.Success ? null : HorseHeaders.ERROR);
            await SendMessage(ack);
        }

        #endregion
    }
}