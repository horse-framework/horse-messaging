using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Cluster;

[assembly: InternalsVisibleTo("Horse.Messaging.Server.WebSocket.Server")]

namespace Horse.Messaging.Server.Clients
{
    /// <summary>
    /// Horse Server client
    /// </summary>
    public class MessagingClient : HorseServerSocket, ISwitchingProtocolClient
    {
        #region Properties

        /// <summary>
        /// Queues that client subscribed
        /// </summary>
        private readonly List<QueueClient> _queues = new List<QueueClient>();

        /// <summary>
        /// Channels that client subscribed
        /// </summary>
        private readonly List<ChannelClient> _channels = new List<ChannelClient>();

        /// <summary>
        /// Connection data of the client.
        /// This keeps first (hello) message from client and keeps method, path and initial properties
        /// </summary>
        public ConnectionData Data { get; internal set; }

        /// <summary>
        /// If true, client authenticated by server's IClientAuthenticator implementation
        /// </summary>
        public bool IsAuthenticated { get; internal set; }

        /// <summary>
        /// Client's unique id.
        /// If it's null or empty, server will create new unique id for the client
        /// </summary>
        public string UniqueId { get; internal set; }

        /// <summary>
        /// Client name
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Client type.
        /// If different type of clients join your server, you can categorize them with this type value.
        /// </summary>
        public string Type { get; internal set; }

        /// <summary>
        /// Client auhentication token.
        /// Usually bearer token.
        /// </summary>
        public string Token { get; internal set; }

        /// <summary>
        /// Extra tag object for developer-usage and is not used by Horse MQ
        /// If you want to keep some data belong the client, you can use this property.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// True, if client is another node
        /// </summary>
        internal bool IsNodeClient { get; set; }

        internal NodeClient NodeClient { get; set; }

        /// <summary>
        /// Messaging queue server
        /// </summary>
        public HorseRider HorseRider { get; }

        /// <summary>
        /// The time instance created
        /// </summary>
        public DateTime ConnectedDate { get; } = DateTime.UtcNow;

        /// <summary>
        /// Remote host of the node server
        /// </summary>
        public string RemoteHost { get; set; }

        /// <summary>
        /// Custom protocol for the client
        /// </summary>
        public ISwitchingProtocol SwitchingProtocol { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MessagingClient(HorseRider server, IConnectionInfo info) : base(server.Server, info)
        {
            HorseRider = server;
            IsConnected = true;
        }

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MessagingClient(HorseRider server, IConnectionInfo info, IUniqueIdGenerator generator)
            : base(server.Server, info, generator)
        {
            HorseRider = server;
            IsConnected = true;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Gets all subscribed queues of client
        /// </summary>
        public IEnumerable<QueueClient> GetQueues()
        {
            List<QueueClient> list;

            lock (_queues)
                list = new List<QueueClient>(_queues);

            return list;
        }

        /// <summary>
        /// Adds queue into client's subscription list
        /// </summary>
        internal void AddSubscription(QueueClient queue)
        {
            lock (_queues)
                _queues.Add(queue);
        }

        /// <summary>
        /// Adds channel into client's subscription list
        /// </summary>
        internal void AddSubscription(ChannelClient channel)
        {
            lock (_channels)
                _channels.Add(channel);
        }

        /// <summary>
        /// Removes queue from client's subscription list
        /// </summary>
        internal void RemoveSubscription(QueueClient queue)
        {
            lock (_queues)
                _queues.Remove(queue);
        }

        /// <summary>
        /// Removes channel from client's subscription list
        /// </summary>
        internal void RemoveSubscription(ChannelClient channel)
        {
            lock (_channels)
                _channels.Remove(channel);
        }

        /// <summary>
        /// Unsubscribes from all queues
        /// </summary>
        internal void UnsubscribeFromAllQueues()
        {
            List<QueueClient> list;
            lock (_queues)
            {
                list = new List<QueueClient>(_queues);
                _queues.Clear();
            }

            foreach (QueueClient cc in list)
                cc.Queue?.RemoveClientSilent(cc);
        }

        /// <summary>
        /// Unsubscribes from all channels
        /// </summary>
        internal void UnsubscribeFromAllChannels()
        {
            List<ChannelClient> list;
            lock (_channels)
            {
                list = new List<ChannelClient>(_channels);
                _channels.Clear();
            }

            foreach (ChannelClient cc in list)
                cc.Channel?.RemoveClientSilent(cc);
        }

        #endregion

        #region Protocol Implementation

        /// <summary>
        /// Sends PING Message to the client
        /// </summary>
        public override void Ping()
        {
            if (SwitchingProtocol != null)
                SwitchingProtocol.Ping();
            else
                base.Ping();
        }

        /// <summary>
        /// Sends PONG Message to the client
        /// </summary>
        public override void Pong(object pingMessage = null)
        {
            if (SwitchingProtocol != null)
                SwitchingProtocol.Pong(pingMessage);
            else
                base.Pong(pingMessage);
        }

        /// <summary>
        /// Sends Horse Message to the client
        /// </summary>
        public override bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (SwitchingProtocol != null)
                return SwitchingProtocol.Send(message, additionalHeaders);

            return base.Send(message, additionalHeaders);
        }

        /// <summary>
        /// Sends Horse Message to the client
        /// </summary>
        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (SwitchingProtocol != null)
                return SwitchingProtocol.SendAsync(message, additionalHeaders);

            return base.SendAsync(message, additionalHeaders);
        }

        #endregion
    }
}