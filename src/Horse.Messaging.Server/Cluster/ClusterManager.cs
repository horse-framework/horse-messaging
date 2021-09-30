using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
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
        public NodeClient[] Clients { get; private set; }

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

        private bool _askingForMain;
        private DateTime _askingForMainExpiration = DateTime.UtcNow;
        private readonly object _askLock = new();
        private Thread _stateThread;

        #endregion

        internal ClusterManager(HorseRider rider)
        {
            Id = Guid.NewGuid().ToString();
            Rider = rider;

            ConnectedToRemoteNodeEvent = new EventManager(rider, HorseEventType.ConnectedToRemoteNode);
            DisconnectedFromRemoteNodeEvent = new EventManager(rider, HorseEventType.DisconnectedFromRemoteNode);
            RemoteNodeConnectEvent = new EventManager(rider, HorseEventType.RemoteNodeConnect);
            RemoteNodeDisconnectEvent = new EventManager(rider, HorseEventType.RemoteNodeDisconnect);
        }

        internal void Initialize()
        {
            _stateThread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1500);

                    if (Clients.Length > 0 && (State == NodeState.Successor || State == NodeState.Replica))
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

                    if (Rider?.Server != null && !Rider.Server.IsRunning)
                        break;
                }
            });

            _stateThread.Start();
        }

        internal void Start()
        {
            List<NodeClient> clients = new List<NodeClient>();

            foreach (NodeInfo nodeInfo in Options.Nodes)
                clients.Add(new NodeClient(Rider, nodeInfo));

            Clients = clients.ToArray();

            foreach (NodeClient client in Clients)
                client.Start();
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

        internal async Task CheckBecomingMainOpportunity()
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

            NodeClient node = Clients.Where(x => x.IsConnected)
                .OrderBy(x => x.ConnectedDate)
                .FirstOrDefault();

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
            message.SetStringContent(System.Text.Json.JsonSerializer.Serialize(announcement));

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
                _askingForMainExpiration = DateTime.UtcNow.AddSeconds(3);
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
        public async Task AnswerMainRequest(NodeClient successor)
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
                    approve = true;
                    break;
            }

            HorseMessage message = new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.MainAnnouncementAnswer);
            message.SetStringContent(approve ? "1" : "0");

            await successor.SendMessage(message);

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Main request of {successor.Info.Name} has {(approve ? "approved" : "rejected")}");

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
                NodeClient firstReplica = Clients.Where(x => x.IsConnected).OrderBy(x => x.Info.Id).FirstOrDefault();

                //if there is no avaiable replica, the node is alone!
                if (firstReplica == null)
                {
                    UpdateState();
                    return Task.CompletedTask;
                }

                //if next replica is self, ask for main
                List<string> compare = new List<string> {Id, firstReplica.Info.Id};
                string firstId = compare.OrderBy(x => x).FirstOrDefault();

                if (firstId == Id)
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

        internal async Task<bool> SendQueueMessageToNodes(HorseMessage message)
        {
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

                case ReplicaAcknowledge.AllNodes:

                    List<Task<bool>> tasks = new List<Task<bool>>();

                    foreach (NodeClient client in Clients)
                    {
                        if (!client.IsConnected)
                            continue;

                        Task<bool> task = client.SendMessageAndWaitAck(clone);
                        tasks.Add(task);
                    }

                    await Task.WhenAll(tasks);
                    return tasks.All(x => x.Result);

                default:
                    return false;
            }
        }

        internal void SendPutBackToNodes(HorseQueue queue, HorseMessage message, bool end)
        {
            HorseMessage msg = new HorseMessage(MessageType.Cluster, queue.Name, KnownContentTypes.NodePutBackQueueMessage);
            msg.SetMessageId(message.MessageId);
            msg.SetStringContent(end ? "1" : "0");

            foreach (NodeClient client in Clients)
            {
                if (!client.IsConnected)
                    continue;

                _ = client.SendMessage(msg);
            }
        }

        internal void SendMessageRemovalToNodes(HorseQueue queue, HorseMessage message)
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

        #endregion

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

        /// <summary>
        /// Sends a queue sync message to the main node
        /// </summary>
        public Task SendQueueSyncRequest(HorseQueue queue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a queue sync message to the main node
        /// </summary>
        public Task SendQueueMessageIdList(NodeClient replica, string queueName, IEnumerable<string> idList)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string id in idList)
                builder.AppendLine(id);

            HorseMessage message = new HorseMessage(MessageType.Cluster, queueName, KnownContentTypes.NodeQueueMessageIdList);
            message.SetStringContent(builder.ToString());

            return replica.SendMessage(message);
        }

        /// <summary>
        /// Sends queue message request to the main to receive missing messages
        /// </summary>
        public Task SendQueueMessageRequest(HorseQueue queue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends queue message response to replica node
        /// </summary>
        public Task SendQueueMessageResponse(HorseQueue queue, NodeClient replica)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a queue sync completion message to the main node
        /// </summary>
        public Task SendQueueSyncCompletion(HorseQueue queue)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}