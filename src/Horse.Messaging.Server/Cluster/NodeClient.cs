using System;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Cluster
{
    public class NodeClient
    {
        private readonly HorseClient _outgoingClient;
        private MessagingClient _incomingClient;

        public NodeInfo Info { get; }
        public HorseRider Rider { get; }
        internal bool ApprovedMainity { get; set; }
        public DateTime ConnectedDate { get; private set; }

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

        public NodeClient(HorseRider rider, NodeInfo info)
        {
            Rider = rider;
            Info = info;

            _outgoingClient = new HorseClient();
            _outgoingClient.ReconnectWait = TimeSpan.FromMilliseconds(500);
            _outgoingClient.RemoteHost = info.Host;

            _outgoingClient.SetClientName(Rider.Options.Name);
            _outgoingClient.SetClientType(Rider.Options.Type);
            _outgoingClient.SetClientToken(rider.Cluster.Options.SharedSecret);

            _outgoingClient.AddProperty(HorseHeaders.HORSE_NODE, HorseHeaders.YES);
            _outgoingClient.AddProperty(HorseHeaders.NODE_ID, rider.Cluster.Id);
            _outgoingClient.AddProperty(HorseHeaders.NODE_HOST, rider.Cluster.Options.NodeHost);
            _outgoingClient.AddProperty(HorseHeaders.NODE_PUBLIC_HOST, rider.Cluster.Options.PublicHost);

            _outgoingClient.MessageReceived += ProcessReceivedMessage;
            _outgoingClient.Disconnected += OutgoingClientOnDisconnected;
            _outgoingClient.Connected += OutgoingClientOnConnected;
        }

        internal void Start()
        {
            _ = _outgoingClient.ConnectAsync();
        }

        private void OutgoingClientOnConnected(HorseClient client)
        {
            if (_incomingClient == null || !_incomingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;
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

            HorseMessage infoMessage = new HorseMessage(MessageType.Cluster, Rider.Options.Name, KnownContentTypes.NodeInformation);
            infoMessage.AddHeader(HorseHeaders.CLIENT_NAME, Rider.Cluster.Options.Name);
            infoMessage.AddHeader(HorseHeaders.NODE_ID, Rider.Cluster.Id);
            infoMessage.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, Rider.Cluster.Options.PublicHost);

            incomingClient.Send(infoMessage);

            if (Rider.Cluster.State == NodeState.Main)
                _ = Rider.Cluster.AnnounceMainity();
        }

        private void OutgoingClientOnDisconnected(HorseClient client)
        {
            if (_incomingClient == null || !_incomingClient.IsConnected)
                ProcessDisconnection();
        }

        private void IncomingClientOnDisconnected(SocketBase client)
        {
            if (_outgoingClient == null || !_outgoingClient.IsConnected)
                ProcessDisconnection();
        }

        private void ProcessDisconnection()
        {
            ClusterManager cluster = Rider.Cluster;

            if (cluster.MainNode != null && cluster.MainNode.Id == Info?.Id)
                _ = cluster.OnMainDown(this);

            else if (cluster.SuccessorNode != null && cluster.SuccessorNode.Id == Info?.Id)
                _ = cluster.OnSuccessorDown(this);
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
                case MessageType.Response:
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
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueListResponse:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueSyncRequest:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueMessageIdList:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueMessageRequest:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueMessageResponse:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.NodeQueueSyncCompletion:
                    throw new NotImplementedException();
                    break;

                #endregion
            }
        }

        internal Task SendMessage(HorseMessage message)
        {
            if (_outgoingClient != null && _outgoingClient.IsConnected)
                return _outgoingClient.SendAsync(message);

            if (_incomingClient != null && _incomingClient.IsConnected)
                return _incomingClient.SendAsync(message);

            return Task.CompletedTask;
        }
    }
}