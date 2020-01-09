using System.Collections.Generic;
using Twino.Core;

namespace Twino.Server.Containers
{
    /// <summary>
    /// Default client container for HttpServer
    /// </summary>
    public class ClientContainer : IClientContainer
    {
        /// <summary>
        /// Online clients
        /// </summary>
        private List<SocketBase> Clients { get; }

        /// <summary>
        /// Creates new client container
        /// </summary>
        public ClientContainer()
        {
            Clients = new List<SocketBase>();
        }

        /// <summary>
        /// Adds the client to the online clients container
        /// </summary>
        public void Add(SocketBase client)
        {
            lock (Clients)
                Clients.Add(client);
        }

        /// <summary>
        /// Gets online client count
        /// </summary>
        public int Count()
        {
            return Clients.Count;
        }

        /// <summary>
        /// Gets all online clients
        /// </summary>
        public IEnumerable<SocketBase> List()
        {
            List<SocketBase> clients = new List<SocketBase>();

            lock (Clients)
                foreach (SocketBase client in Clients)
                    clients.Add(client);

            return clients;
        }

        /// <summary>
        /// Removes the client from the online clients container
        /// </summary>
        public void Remove(SocketBase client)
        {
            lock (Clients)
                Clients.Remove(client);
        }
    }
}