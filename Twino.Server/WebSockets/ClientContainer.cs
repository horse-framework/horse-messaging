using System.Collections.Generic;

namespace Twino.Server.WebSockets
{
    /// <summary>
    /// Default client container for HttpServer
    /// </summary>
    public class ClientContainer : IClientContainer
    {
        /// <summary>
        /// Online clients
        /// </summary>
        private List<ServerSocket> Clients { get; }

        public ClientContainer()
        {
            Clients = new List<ServerSocket>();
        }

        /// <summary>
        /// Adds the client to the online clients container
        /// </summary>
        public void Add(ServerSocket client)
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
        public IEnumerable<ServerSocket> List()
        {
            List<ServerSocket> clients = new List<ServerSocket>();

            lock (Clients)
                foreach (ServerSocket client in Clients)
                    clients.Add(client);

            return clients;
        }

        /// <summary>
        /// Removes the client from the online clients container
        /// </summary>
        public void Remove(ServerSocket client)
        {
            lock (Clients)
                Clients.Remove(client);
        }
    }
}