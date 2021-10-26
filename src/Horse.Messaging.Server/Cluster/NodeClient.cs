using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Describes a remote node
    /// </summary>
    public class NodeClient
    {
        private readonly HorseClient _outgoingClient;
        private readonly NodeDeliveryTracker _deliveryTracker;
        private MessagingClient _incomingClient;
        private bool _connectedToRemote;

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
        internal bool IsConnected
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

            _outgoingClient.MessageReceived += ProcessReceivedMessage;
            _outgoingClient.Disconnected += OutgoingClientOnDisconnected;
            _outgoingClient.Connected += OutgoingClientOnConnected;

            _deliveryTracker = new NodeDeliveryTracker(this);
            _deliveryTracker.Run();
        }

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

            if (_outgoingClient == null || !_outgoingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;

            incomingClient.Disconnected += IncomingClientOnDisconnected;

            HorseMessage infoMessage = new HorseMessage(MessageType.Cluster, Rider.Cluster.Options.Name, KnownContentTypes.NodeInformation);
            infoMessage.AddHeader(HorseHeaders.CLIENT_NAME, Rider.Cluster.Options.Name);
            infoMessage.AddHeader(HorseHeaders.NODE_ID, Rider.Cluster.Id);
            infoMessage.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, Rider.Cluster.Options.PublicHost);

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

            ClusterManager cluster = Rider.Cluster;

            if (cluster.Options.Mode == ClusterMode.Scaled)
                return;

            if (cluster.MainNode != null && cluster.MainNode.Id == Info?.Id)
                await cluster.OnMainDown(this);

            else if (cluster.SuccessorNode != null && cluster.SuccessorNode.Id == Info?.Id)
                await cluster.OnSuccessorDown(this);

            cluster.UpdateState();
        }

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
                    MainNodeAnnouncement msg = System.Text.Json.JsonSerializer.Deserialize<MainNodeAnnouncement>(message.GetStringContent());
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

                case KnownContentTypes.NodeQueueListRequest:
                    if (cluster.State == NodeState.Main)
                    {
                        List<NodeQueueInfo> infoList = Rider.Queue.Queues.Select(x => x.CreateNodeQueueInfo()).ToList();
                        HorseMessage listMessage = new HorseMessage(MessageType.Cluster, Info.Id, KnownContentTypes.NodeQueueListResponse);
                        listMessage.SetStringContent(System.Text.Json.JsonSerializer.Serialize(infoList));
                        _ = SendMessage(listMessage);
                    }

                    break;

                case KnownContentTypes.NodeQueueListResponse:
                {
                    List<NodeQueueInfo> infoList = System.Text.Json.JsonSerializer.Deserialize<List<NodeQueueInfo>>(message.GetStringContent());
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
                        _ = queue.Manager.Synchronizer.ProcessReceivedMessages(message);

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
                    NodeQueueInfo queueInfo = System.Text.Json.JsonSerializer.Deserialize<NodeQueueInfo>(content);
                    _ = Rider.Queue.CreateReplica(queueInfo);
                    break;
                }

                case KnownContentTypes.UpdateQueue:
                {
                    HorseQueue queue = Rider.Queue.Find(message.Target);

                    if (queue != null)
                    {
                        string content = message.GetStringContent();
                        NodeQueueInfo queueInfo = System.Text.Json.JsonSerializer.Deserialize<NodeQueueInfo>(content);
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
                        _ = queue.Manager.RemoveMessage(message.MessageId);

                    break;
                }

                #endregion
            }
        }

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
            HorseQueue queue = Rider.Queue.Find(message.Target);

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
    }
}