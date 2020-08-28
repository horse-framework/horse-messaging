using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        /// Queues that client subscribed
        /// </summary>
        private readonly List<QueueClient> _queues = new List<QueueClient>();

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
        /// Extra tag object for developer-usage and is not used by Twino MQ
        /// If you want to keep some data belong the client, you can use this property.
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// Messaging queue server
        /// </summary>
        public TwinoMQ TwinoMq { get; }

        /// <summary>
        /// The time instance created
        /// </summary>
        public DateTime ConnectedDate { get; } = DateTime.UtcNow;

        /// <summary>
        /// Remote host of the node server
        /// </summary>
        public string RemoteHost { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MqClient(TwinoMQ server, IConnectionInfo info) : base(server.Server, info)
        {
            TwinoMq = server;
            IsConnected = true;
        }

        /// <summary>
        /// Creates new MQ Client
        /// </summary>
        public MqClient(TwinoMQ server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(server.Server, info, generator, useUniqueMessageId)
        {
            TwinoMq = server;
            IsConnected = true;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Gets all queues of the server
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
        internal void Join(QueueClient queue)
        {
            lock (_queues)
                _queues.Add(queue);
        }

        /// <summary>
        /// Removes queue from client's subscription list
        /// </summary>
        internal void Leave(QueueClient queue)
        {
            lock (_queues)
                _queues.Remove(queue);
        }

        /// <summary>
        /// Unsubscribes from all queues
        /// </summary>
        internal async Task UnsubscribeFromAllQueues()
        {
            List<QueueClient> list;
            lock (_queues)
            {
                list = new List<QueueClient>(_queues);
                _queues.Clear();
            }

            foreach (QueueClient cc in list)
                if (cc.Queue != null)
                    await cc.Queue.RemoveClientSilent(cc);
        }

        #endregion
    }
}