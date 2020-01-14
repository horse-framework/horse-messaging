using System.Collections.Generic;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Clients
{
    /// <summary>
    /// TMQ Server client for Twino MQ
    /// </summary>
    public class MqClient : TmqServerSocket
    {
        #region Properties

        /// <summary>
        /// Channels that client is in
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
        /// If it's null or empty, server will create new unique id for the client.
        /// </summary>
        public string UniqueId { get; internal set; }

        /// <summary>
        /// Client name.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// True, if the client is a instanced server
        /// </summary>
        public bool IsInstanceServer { get; internal set; }

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
        /// Extra tag object for developer-usage and is not used by Twino MQ
        /// If you want to keep some data belong the client, you can use this property.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Messaging queue server
        /// </summary>
        public MqServer MqServer { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MqClient(MqServer server, IConnectionInfo info) : base(server.Server, info)
        {
            MqServer = server;
            IsConnected = true;
        }

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MqClient(MqServer server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(server.Server, info, generator, useUniqueMessageId)
        {
            MqServer = server;
            IsConnected = true;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Gets all channels of the client
        /// </summary>
        public IEnumerable<ChannelClient> GetChannels()
        {
            List<ChannelClient> list;

            lock (_channels)
                list = new List<ChannelClient>(_channels);

            return list;
        }

        /// <summary>
        /// Adds channel into client's channel list
        /// </summary>
        internal void Join(ChannelClient channel)
        {
            lock (_channels)
                _channels.Add(channel);
        }

        /// <summary>
        /// Removes channel from client's channel list
        /// </summary>
        internal void Leave(ChannelClient channel)
        {
            lock (_channels)
                _channels.Remove(channel);
        }

        #endregion
    }
}