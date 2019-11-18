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
        private List<ServerSocketBase> Clients { get; }

        public ClientContainer()
        {
            Clients = new List<ServerSocketBase>();
        }

        /// <summary>
        /// Adds the client to the online clients container
        /// </summary>
        public void Add(ServerSocketBase client)
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
        public IEnumerable<ServerSocketBase> List()
        {
            List<ServerSocketBase> clients = new List<ServerSocketBase>();

            lock (Clients)
                foreach (ServerSocketBase client in Clients)
                    clients.Add(client);

            return clients;
        }

        /// <summary>
        /// Removes the client from the online clients container
        /// </summary>
        public void Remove(ServerSocketBase client)
        {
            lock (Clients)
                Clients.Remove(client);
        }
    }
}