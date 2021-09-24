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
            throw new NotImplementedException();
        }

        private void OutgoingClientOnDisconnected(HorseClient client)
        {
            throw new NotImplementedException();
        }

        internal void IncomingClientConnected(MessagingClient incomingClient)
        {
            _incomingClient = incomingClient;
            incomingClient.NodeClient = this;
            incomingClient.IsNodeClient = true;
            incomingClient.Disconnected += IncomingClientOnDisconnected;
        }

        private void IncomingClientOnDisconnected(SocketBase client)
        {
            throw new NotImplementedException();
        }

        internal void ProcessReceivedMessage(HorseClient client, HorseMessage message)
        {
            ProcessMessage(message);
        }

        internal void ProcessReceivedMessage(MessagingClient client, HorseMessage message)
        {
            ProcessMessage(message);
        }

        private void ProcessMessage(HorseMessage message)
        {
            switch (message.ContentType)
            {
                #region Node Management

                case KnownContentTypes.NodeHandshake:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.MainNodeAnnouncement:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.MainAnnouncementAnswer:
                    throw new NotImplementedException();
                    break;

                case KnownContentTypes.AskForMainPermission:
                    throw new NotImplementedException();
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

        public Task SendMessage(HorseMessage message)
        {
            if (_outgoingClient != null && _outgoingClient.IsConnected)
                return _outgoingClient.SendAsync(message);
            else if (_incomingClient != null && _incomingClient.IsConnected)
                return _incomingClient.SendAsync(message);
        }
    }
}