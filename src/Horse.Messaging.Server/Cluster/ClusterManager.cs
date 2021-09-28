using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Cluster
{
    public class ClusterManager
    {
        #region Properties

        public string Id { get; }
        public HorseRider Rider { get; }

        public ClusterOptions Options { get; } = new();

        public NodeState State { get; private set; }

        public NodeInfo MainNode { get; set; }
        public NodeInfo SuccessorNode { get; set; }

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

        private int _requiredApprovement = 0;
        private int _recievedApprovement = 0;
        private bool _askingForMain = false;
        private DateTime _askingForMainExpiration = DateTime.UtcNow;
        private object _askLock = new object();

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

        private void UpdateState()
        {
            if (!Clients.Any(x => x.IsConnected))
            {
                State = NodeState.Single;
                return;
            }

            if (MainNode != null && MainNode.Id == Id)
                State = NodeState.Main;
            else if (SuccessorNode != null && SuccessorNode.Id == Id)
                State = NodeState.Successor;
            else
                State = NodeState.Replica;
        }

        private NodeInfo FindSuccessor()
        {
            if (State != NodeState.Main)
                return null;

            if (Clients.Length == 0)
                return null;

            if (Clients.Length == 1)
            {
                NodeClient node = Clients[0];
                return node?.Info;
            }

            return null;
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

            Rider.Server.Logger?.LogEvent("CLUSTER", "The node has become Main");
        }

        /// <summary>
        /// Asks to the successor for it's main announcement
        /// </summary>
        public async Task AskForMain()
        {
            lock (_askLock)
            {
                if (_askingForMain)
                {
                    if (_askingForMainExpiration > DateTime.UtcNow)
                        return;
                }

                _askingForMain = true;
            }

            _requiredApprovement = Clients.Count(x => x.IsConnected);
            _recievedApprovement = 0;

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

            if (State != NodeState.Main && MainNode != null)
            {
                NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id);

                if (!mainClient.IsConnected)
                    approve = true;
            }

            HorseMessage message = new HorseMessage(MessageType.Cluster, Id, KnownContentTypes.MainAnnouncementAnswer);
            message.SetStringContent(approve ? "1" : "0");

            await successor.SendMessage(message);

            Rider.Server.Logger?.LogEvent("CLUSTER", $"Main request of {successor.Info.Name} has {(approve ? "approved" : "rejected")}");
        }

        #endregion

        #region Events

        internal Task OnMainDown(NodeClient mainClient)
        {
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
            UpdateState();

            //ignore, if main still there
            if (MainNode != null)
            {
                NodeClient mainClient = Clients.FirstOrDefault(x => x.Info.Id == MainNode.Id);

                if (mainClient.IsConnected)
                    return Task.CompletedTask;
            }

            return AskForMain();
        }

        internal Task OnSuccessorDown(NodeClient successor)
        {
            Rider.Server.Logger?.LogEvent("CLUSTER", $"Successor node is down: {successor.Info.Name}");

            UpdateState();

            if (State == NodeState.Main)
                return AnnounceMainity();

            return Task.CompletedTask;
        }

        internal Task OnMainRequested(NodeClient requestedClient)
        {
            return AnswerMainRequest(requestedClient);
        }

        internal Task OnRequestAnswered(NodeClient client, bool approved)
        {
            lock (_askLock)
            {
                if (!approved)
                {
                    _askingForMain = false;
                    client.ApprovedMainity = false;
                    return Task.CompletedTask;
                }

                if (client.ApprovedMainity)
                    return Task.CompletedTask;

                client.ApprovedMainity = true;
                _recievedApprovement++;

                if (_recievedApprovement == _requiredApprovement && (State == NodeState.Main || State == NodeState.Successor))
                    return AnnounceMainity();

                return Task.CompletedTask;
            }
        }

        internal void OnMainAnnounced(NodeClient announcer, MainNodeAnnouncement announcement)
        {
            MainNode = announcement.Main;
            SuccessorNode = announcement.Successor;
            UpdateState();

            Rider.Server.Logger?.LogEvent("CLUSTER", $"New Main node is announce by {announcement.Main.Name} with successor {announcement.Successor?.Name}");
        }

        #endregion

        #region Messaging Operations

        internal void ProcessMessageFromClient(MessagingClient client, HorseMessage message)
        {
            if (Options.Mode == ClusterMode.Scaled)
                foreach (NodeClient node in Clients)
                    _ = node.SendMessage(message);
        }

        internal Task SendQueueMessageToNodes()
        {
            throw new NotImplementedException();
        }

        internal Task SendPutBackToNodes()
        {
            throw new NotImplementedException();
        }

        internal Task SendMessageRemovalToNodes()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Queue Sync

        /// <summary>
        /// Sends queue list request message to the main for sync
        /// </summary>
        public Task RequestQueueListForSync()
        {
            throw new NotImplementedException();
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