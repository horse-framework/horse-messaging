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
        /// <summary>
        /// Queues that client is subscribed
        /// </summary>
        private readonly List<QueueClient> _queues = new List<QueueClient>();

        /// <summary>
        /// Connection data of the client.
        /// This keeps first (hello) message from client and keeps method, path and initial properties
        /// </summary>
        public ConnectionData Data { get; internal set; }

        /// <summary>
        /// Client information
        /// </summary>
        public ClientInformation Info { get; internal set; }

        /// <summary>
        /// If true, client authenticated by server's IClientAuthenticator implementation
        /// </summary>
        public bool IsAuthenticated { get; internal set; }

        /// <summary>
        /// Extra tag object for developer-usage and is not used by Twino MQ
        /// If you want to keep some data belong the client, you can use this property.
        /// </summary>
        public object Tag { get; set; }

        public MqClient(ITwinoServer server, IConnectionInfo info) : base(server, info)
        {
        }

        public MqClient(ITwinoServer server, IConnectionInfo info, IUniqueIdGenerator generator, bool useUniqueMessageId = true)
            : base(server, info, generator, useUniqueMessageId)
        {
        }

        /// <summary>
        /// Gets all queues of the client
        /// </summary>
        public IEnumerable<QueueClient> GetQueues()
        {
            List<QueueClient> list = new List<QueueClient>();
            lock (_queues)
                foreach (QueueClient queue in _queues)
                    list.Add(queue);

            return list;
        }
    }
}