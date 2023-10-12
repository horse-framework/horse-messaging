using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Horse Cluster Manager
    /// </summary>
    public class ClusterManager
    {
        #region Properties

        /// <summary>
        /// Node unique id
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Cluster Options
        /// </summary>
        public ClusterOptions Options { get; } = new();

        /// <summary>
        /// Node state
        /// </summary>
        public NodeState State { get; private set; }

        /// <summary>
        /// Cluster's main node information
        /// </summary>
        public NodeInfo MainNode { get; set; }

        /// <summary>
        /// Cluster's successor node information
        /// </summary>
        public NodeInfo SuccessorNode { get; set; }

        /// <summary>
        /// Other nodes in cluster
        /// </summary>
        public NodeClient[] Clients { get; private set; } = Array.Empty<NodeClient>();

        /// <summary>
        /// Event Manager for HorseEventType.ConnectedToRemoteNode 
        /// </summary>
        public EventManager ConnectedToRemoteNodeEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.DisconnectedFromRemoteNode 
        /// </summary>
        public EventManager DisconnectedFromRemoteNodeEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.RemoteNodeConnect 
        /// </summary>
        public EventManager RemoteNodeConnectEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.RemoteNodeDisconnect 
        /// </summary>
        public EventManager RemoteNodeDisconnectEvent { get; }

        /// <summary>
        /// The time cluster is initialized
        /// </summary>
        public DateTime StartDate { get; internal set; }

        internal DateTime QueueUpdate { get; set; }

        private bool _askingForMain;
        private DateTime _askingForMainExpiration = DateTime.UtcNow;
        private readonly object _askLock = new();
        private Thread _stateThread;

        private readonly SemaphoreSlim _queueSyncSlim = new SemaphoreSlim(1, 1);

        #endregion

        internal ClusterManager(HorseRider rider)
        {
            Id = $"{Guid.NewGuid()}";
            Rider = rider;
            StartDate = DateTime.UtcNow;

            ConnectedToRemoteNodeEvent = new EventManager(rider, HorseEventType.ConnectedToRemoteNode);
            DisconnectedFromRemoteNodeEvent = new EventManager(rider, HorseEventType.DisconnectedFromRemoteNode);
            RemoteNodeConnectEvent = new EventManager(rider, HorseEventType.RemoteNodeConnect);
            RemoteNodeDisconnectEvent = new EventManager(rider, HorseEventType.RemoteNodeDisconnect);
        }

        internal void Initialize()
        {
            _stateThread = new Thread(() =>
            {
                Random rnd = new Random();

                while (Rider?.Server == null || !Rider.Server.IsRunning)
                    Thread.Sleep(500);

                while (Rider.Server.IsRunning)
                {
                    Thread.Sleep(rnd.Next(2500, 7750));

                    if (Options.Nodes.Count == 0 || Options.Mode == ClusterMode.Scaled)
                        return;

                    if (Clients.Length > 0 && (State == NodeState.Successor || State == NodeState.Replica || State == NodeState.Single))
                    {
                        if (MainNode == null)
                            _ = AskForMain();
                        else
                        {
                            NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id && x.IsConnected);
                            if (mainClient == null)
                                _ = AskForMain();
                        }
                    }
                }
            });
        }

        internal void Start()
        {
            StartDate = DateTime.UtcNow;

            if (Options.Nodes.Count == 0)
            {
                Options.Mode = ClusterMode.Scaled;
                return;
            }

            List<NodeClient> clients = new List<NodeClient>();

            foreach (NodeInfo nodeInfo in Options.Nodes)
                clients.Add(new NodeClient(Rider, nodeInfo));

            Clients = clients.ToArray();

            foreach (NodeClient client in Clients)
                client.Start();

            _stateThread.Start();
        }

        /// <summary>
        /// Returns false, if the client trying to connect before cluster initialized
        /// </summary>
        public bool CanClientConnect()
        {
            if (Options.Nodes.Count == 0)
                return true;

            if (Options.Mode == ClusterMode.Scaled)
                return true;

            TimeSpan lifetime = DateTime.UtcNow - StartDate;
            if (lifetime > TimeSpan.FromSeconds(15))
                return true;

            if (State == NodeState.Main)
                return true;

            return false;
        }

        #region Node Management

        internal void UpdateState()
        {
            NodeState prev = State;

            if (Clients.Length == 0 || Clients.All(x => !x.IsConnected))
                State = NodeState.Single;
            else if (MainNode != null && MainNode.Id == Id)
                State = NodeState.Main;
            else if (SuccessorNode != null && SuccessorNode.Id == Id)
                State = NodeState.Successor;
            else if (MainNode == null && SuccessorNode == null)
                State = NodeState.Single;
            else
                State = NodeState.Replica;

            if (State != prev)
            {
                if (State == NodeState.Replica || State == NodeState.Successor)
                    Rider.Client.DisconnectAllClients();

                Rider.Server.Logger?.LogEvent("CLUSTER", $"The Node is {State}");
            }

            if (MainNode != null)
            {
                NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id);
                if (mainClient == null || !mainClient.IsConnected)
                    _ = CheckBecomingMainOpportunity();
            }
        }

        private async Task CheckBecomingMainOpportunity()
        {
            NodeClient remoteClient = Clients.FirstOrDefault(x => x.IsConnected);
            HorseMessage whoIsMain = new HorseMessage(MessageType.Cluster, Rider.Cluster.Options.Name, KnownContentTypes.WhoIsMainNode);
            await remoteClient.SendMessage(whoIsMain);
            await Task.Delay(new Random().Next(250, 1000));

            if (MainNode == null)
                _ = AskForMain();
        }

        private NodeInfo FindSuccessor()
        {
            if (Clients.Length == 0)
                return null;

            NodeClient node = Clients.Where(x => x.IsConnected).MinBy(x => x.ConnectedDate);
            return node?.Info;
        }

        /// <summary>
        /// Announce self as main node and successor
        /// </summary>
        public async Task AnnounceMainity()
        {
            MainNodeAnnouncement announcement = new MainNodeAnnouncement
            {
                Main = new NodeInfo
                {
                    Id = Id,
                    Name = Options.Name,
                    Host = Options.NodeHost,
                    PublicHost = Options.PublicHost
                },
                Successor = FindSuccessor()
            };

            HorseMessage message = new HorseMessage(MessageType.Cluster, "Node", KnownContentTypes.MainNodeAnnouncement);
            message.SetStringContent(System.Text.Json.JsonSerializer.Serialize(announcement, SerializerFactory.Default()));

            foreach (NodeClient client in Clients)
            {
                if (client.IsConnected)
                    await client.SendMessage(message);
            }

            MainNode = announcement.Main;
            SuccessorNode = announcement.Successor;

            UpdateState();

            Rider.Server.Logger?.LogEvent("CLUSTER", "The node is Main");
        }

        /// <summary>
        /// Asks to the successor for it's main announcement
        /// </summary>
        public async Task AskForMain()
        {
            if (!Clients.Any(x => x.IsConnected))
                return;

            lock (_askLock)
            {
                if (_askingForMain)
                {
                    if (_askingForMainExpiration > DateTime.UtcNow)
                        return;
                }

                _askingForMain = true;
                _askingForMainExpiration = DateTime.UtcNow.AddMilliseconds(new Random().Next(2500, 7500));
            }

            HorseMessage message = new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.AskForMainPermission);

            foreach (NodeClient client in Clients)
            {
                client.ApprovedMainity = false;
                if (client.IsConnected)
                    await client.SendMessage(message);
            }

            Rider.Server.Logger?.LogEvent("CLUSTER", "The node is asking for becoming Main");
        }

        /// <summary>
        /// Sends an answer message to the successor if it can be main or not
        /// </summary>
        private async Task AnswerMainRequest(NodeClient successor)
        {
            bool approve = false;

            switch (State)
            {
                case NodeState.Replica:

                    if (MainNode == null)
                        approve = true;
                    else
                    {
                        NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id);

                        if (mainClient == null || !mainClient.IsConnected)
                            approve = true;
                    }

                    break;

                case NodeState.Single:
                    if (successor.Info.StartDate.HasValue)
                        approve = successor.Info.StartDate <= StartDate;
                    else
                        Rider.Server.Logger?.LogEvent("CLUSTER", $"Main requester has no valid start date");
                    break;
            }

            HorseMessage message = new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.MainAnnouncementAnswer);
            message.SetStringContent(approve ? "1" : "0");

            await successor.SendMessage(message);

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Main request of {successor.Info.Name} has {(approve ? "approved" : "rejected")} as {State}");

            if (State == NodeState.Main)
                await AnnounceMainity();
        }

        #endregion

        #region Events

        internal Task OnMainDown(NodeClient mainClient)
        {
            if (Options.Mode == ClusterMode.Scaled)
                return Task.CompletedTask;

            MainNode = null;
            Rider.Server.Logger?.LogEvent("CLUSTER", $"Main node is down: {mainClient.Info.Name}");

            //if the node should be the next main
            if (State == NodeState.Successor || SuccessorNode?.Id == Id)
            {
                return AskForMain();
            }

            //the node is just replica
            //if there is an available successor, we will prod it for being main
            //otherwise we will find next replica to prod
            //if the next replica is that client, we will ask for main
            if (State == NodeState.Replica)
            {
                //find the successor and prod it for being main
                if (SuccessorNode != null)
                {
                    NodeClient successorClient = Clients.FirstOrDefault(x => x.Info.Id == SuccessorNode.Id);

                    if (successorClient != null && successorClient.IsConnected)
                    {
                        HorseMessage message = new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.ProdForMainAnnouncement);
                        return successorClient.SendMessage(message);
                    }
                }

                //successor is not available, find next replica
                NodeClient firstReplica = Clients.Where(x => x.IsConnected).MinBy(x => x.Info.Id);

                //if there is no avaiable replica, the node is alone!
                if (firstReplica == null)
                {
                    UpdateState();
                    return Task.CompletedTask;
                }

                NodeClient oldestClient = Clients.Where(x => x.Info.StartDate.HasValue).MinBy(x => x.Info.StartDate);

                if (StartDate > oldestClient.Info.StartDate)
                    return AskForMain();

                //prod the next replica for being main
                return firstReplica.SendMessage(new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.ProdForMainAnnouncement));
            }

            return Task.CompletedTask;
        }

        internal Task OnProdForAnnouncement()
        {
            if (Options.Mode == ClusterMode.Scaled)
                return Task.CompletedTask;

            UpdateState();

            //ignore, if main still there
            if (MainNode != null)
            {
                NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id);

                if (mainClient != null && mainClient.IsConnected)
                    return Task.CompletedTask;
            }

            return AskForMain();
        }

        internal async Task OnSuccessorDown(NodeClient successor)
        {
            if (Options.Mode == ClusterMode.Scaled)
                return;

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Successor node is down: {successor.Info.Name}");

            UpdateState();

            if (State == NodeState.Main)
            {
                await AnnounceMainity();

                HorseMessage resetMessage = MessageBuilder.StatusCodeMessage(KnownContentTypes.ResetContent);

                if (Rider.Cluster.SuccessorNode != null)
                    resetMessage.AddHeader(HorseHeaders.SUCCESSOR_NODE, Rider.Cluster.SuccessorNode.PublicHost);

                string alternate = string.Empty;
                foreach (NodeClient nodeClient in Rider.Cluster.Clients)
                {
                    if (!nodeClient.IsConnected)
                        continue;

                    if (nodeClient.Info.Id == Rider.Cluster.SuccessorNode?.Id)
                        continue;

                    alternate += alternate.Length == 0 ? nodeClient.Info.PublicHost : $",{nodeClient.Info.PublicHost}";
                }

                if (!string.IsNullOrEmpty(alternate))
                    resetMessage.AddHeader(HorseHeaders.REPLICA_NODE, alternate);

                foreach (MessagingClient client in Rider.Client.Clients)
                    _ = client.SendAsync(resetMessage);
            }
        }

        internal Task OnMainRequested(NodeClient requestedClient)
        {
            if (Options.Mode == ClusterMode.Scaled)
                return Task.CompletedTask;

            return AnswerMainRequest(requestedClient);
        }

        internal Task OnRequestAnswered(NodeClient client, bool approved)
        {
            string answer = approved ? "accepted" : "rejected";
            Rider.Server.Logger?.LogEvent("CLUSTER", $"Main Request {answer} by {client.Info.Name}");

            if (Options.Mode == ClusterMode.Scaled)
                return Task.CompletedTask;

            lock (_askLock)
            {
                if (!approved)
                {
                    _askingForMain = false;
                    client.ApprovedMainity = false;
                    Rider.Server.Logger?.LogEvent("CLUSTER", "Becoming main operation is canceled");
                    return Task.CompletedTask;
                }

                if (client.ApprovedMainity)
                    return Task.CompletedTask;

                client.ApprovedMainity = true;

                if (Clients.Where(x => x.IsConnected).All(x => x.ApprovedMainity))
                    return AnnounceMainity();

                return Task.CompletedTask;
            }
        }

        internal void OnMainAnnounced(NodeClient announcer, MainNodeAnnouncement announcement)
        {
            if (Options.Mode == ClusterMode.Scaled)
                return;

            bool mainHasChanged = MainNode?.Id != announcement.Main.Id;

            MainNode = announcement.Main;
            SuccessorNode = announcement.Successor;
            UpdateState();

            Rider.Server.Logger?.LogEvent("CLUSTER", $"New Main node is announce by {MainNode.Name} with successor {SuccessorNode?.Name}");

            if (State != NodeState.Main && mainHasChanged)
                _ = RequestQueueListForSync();
        }

        #endregion

        #region Messaging Operations

        internal void ProcessMessageFromClient(MessagingClient client, HorseMessage message)
        {
            if (Options.Mode == ClusterMode.Scaled)
                foreach (NodeClient node in Clients)
                    _ = node.SendMessage(message);
        }

        internal async Task<bool> SendQueueMessage(HorseMessage message)
        {
            if (State == NodeState.Single)
                return true;

            HorseMessage clone = message.Clone(true, true, message.MessageId);

            clone.Type = MessageType.Cluster;
            clone.ContentType = KnownContentTypes.NodePushQueueMessage;

            switch (Options.Acknowledge)
            {
                case ReplicaAcknowledge.None:
                    foreach (NodeClient client in Clients)
                        _ = client.SendMessage(clone);

                    return true;

                case ReplicaAcknowledge.OnlySuccessor:
                {
                    if (SuccessorNode == null)
                        return false;

                    NodeClient successorClient = Clients.FirstOrDefault(x => x.Info.Id == SuccessorNode.Id);

                    if (successorClient == null || !successorClient.IsConnected)
                        return false;

                    bool ack = await successorClient.SendMessageAndWaitAck(clone);

                    if (ack)
                    {
                        foreach (NodeClient client in Clients)
                            if (client != successorClient)
                                _ = client.SendMessage(clone);
                    }

                    return ack;
                }

                case ReplicaAcknowledge.AllNodes:
                {
                    if (SuccessorNode == null)
                        return false;

                    NodeClient successorClient = Clients.FirstOrDefault(x => x.Info.Id == SuccessorNode.Id);

                    if (successorClient == null || !successorClient.IsConnected)
                        return false;

                    bool successorAck = await successorClient.SendMessageAndWaitAck(clone);
                    if (!successorAck)
                        return false;

                    List<Task<bool>> tasks = new List<Task<bool>>();

                    foreach (NodeClient client in Clients)
                    {
                        if (!client.IsConnected || client == successorClient)
                            continue;

                        Task<bool> task = client.SendMessageAndWaitAck(clone);
                        tasks.Add(task);
                    }

                    await Task.WhenAll(tasks);
                    return true;
                }

                default:
                    return false;
            }
        }

        internal void SendPutBack(HorseQueue queue, HorseMessage message, bool end)
        {
            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.NodePutBackQueueMessage);
            msg.SetMessageId(message.MessageId);
            msg.SetStringContent(end ? "1" : "0");

            foreach (NodeClient client in Clients)
            {
                if (client == null)
                    continue;

                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        internal void SendMessageRemoval(HorseQueue queue, HorseMessage message)
        {
            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.NodeRemoveQueueMessage);
            msg.SetMessageId(message.MessageId);

            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        internal void SendQueueCreated(HorseQueue queue)
        {
            if (Options.Mode != ClusterMode.Reliable || State != NodeState.Main)
                return;

            NodeQueueInfo info = queue.CreateNodeQueueInfo();
            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.CreateQueue);
            msg.SetStringContent(System.Text.Json.JsonSerializer.Serialize(info, SerializerFactory.Default()));

            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        internal void SendQueueUpdated(HorseQueue queue)
        {
            if (Options.Mode != ClusterMode.Reliable || State != NodeState.Main)
                return;

            NodeQueueInfo info = queue.CreateNodeQueueInfo();
            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.UpdateQueue);
            msg.SetStringContent(System.Text.Json.JsonSerializer.Serialize(info, SerializerFactory.Default()));

            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        internal void SendQueueRemoved(HorseQueue queue)
        {
            if (Options.Mode != ClusterMode.Reliable || State != NodeState.Main)
                return;

            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.RemoveQueue);

            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        #endregion

        internal void SendMessage(HorseMessage message)
        {
            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(message);
            }
        }

        #region Queue Sync

        /// <summary>
        /// Sends queue list request message to the main for sync
        /// </summary>
        public Task RequestQueueListForSync()
        {
            if (MainNode == null)
                return Task.CompletedTask;

            NodeClient mainClient = Clients.FirstOrDefault(x => x.IsConnected && x.Info.Id == MainNode.Id);

            if (mainClient != null)
            {
                HorseMessage message = new HorseMessage(MessageType.Cluster, MainNode.Id, KnownContentTypes.NodeQueueListRequest);
                return mainClient.SendMessage(message);
            }

            return Task.CompletedTask;
        }

        internal async Task ProcessQueueList(NodeClient client, List<NodeQueueInfo> infoList)
        {
            HorseQueue[] queues = Rider.Queue.Queues.ToArray();
            foreach (HorseQueue queue in queues)
            {
                NodeQueueInfo info = infoList.FirstOrDefault(x => x.Name == queue.Name);
                if (info == null)
                    await Rider.Queue.Remove(queue);
            }

            if (_queueSyncSlim.CurrentCount == 0)
            {
                try
                {
                    _queueSyncSlim.Release();
                }
                catch
                {
                }
            }

            foreach (NodeQueueInfo info in infoList)
            {
                HorseQueue queue = queues.FirstOrDefault(x => x.Name == info.Name);
                if (queue == null)
                    queue = await Rider.Queue.CreateReplica(info);
                else
                    queue.UpdateOptionsByNodeInfo(info);

                try
                {
                    await _queueSyncSlim.WaitAsync(TimeSpan.FromMilliseconds(1500));
                    await queue.Manager.Synchronizer.BeginReceiving(client);
                    HorseMessage message = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.NodeQueueSyncRequest);
                    await client.SendMessage(message);
                }
                catch
                {
                }
                finally
                {
                    _queueSyncSlim.Release();
                }
            }
        }

        #endregion
    }
}