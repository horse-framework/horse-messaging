using System;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

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

            _outgoingClient.MessageReceived += ProcessReceivedMessage;
            _outgoingClient.Disconnected += OutgoingClientOnDisconnected;
            _outgoingClient.Connected += OutgoingClientOnConnected;

            _ = _outgoingClient.ConnectAsync();
        }

        private void OutgoingClientOnConnected(HorseClient client)
        {
            if (_incomingClient == null || !_incomingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;

            throw new NotImplementedException();
        }

        internal void IncomingClientConnected(MessagingClient incomingClient)
        {
            _incomingClient = incomingClient;
            incomingClient.NodeClient = this;
            incomingClient.IsNodeClient = true;
            
            if (_outgoingClient == null || !_outgoingClient.IsConnected)
                ConnectedDate = DateTime.UtcNow;
            
            incomingClient.Disconnected += IncomingClientOnDisconnected;
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

                /*todo: 
                case KnownContentTypes.NodeHandshake:
                    Info.Id = message.MessageId;
                    Info.Name = message.Target;

                    //todo: if both main, connector will prod for announcement
                    if (cluster.State == NodeState.Main)
                    {
                    }

                    break;
*/
                
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