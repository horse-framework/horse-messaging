using System.Collections.Generic;
using Twino.Core;

namespace Twino.Server.Containers
{
    /// <summary>
    /// WebSocket Client container implementation of the HttpServer class
    /// Contains online clients.
    /// HttpServer automatically adds the client to the container when it's connected
    /// And removes the client from the container when it's disconnected
    /// </summary>
    public interface IClientContainer
    {
        /// <summary>
        /// Gets online client count
        /// </summary>
        int Count();

        /// <summary>
        /// Adds the client to the online clients container
        /// </summary>
        void Add(SocketBase client);

        /// <summary>
        /// Removes the client from the online clients container
        /// </summary>
        void Remove(SocketBase client);

        /// <summary>
        /// Gets all online clients
        /// </summary>
        IEnumerable<SocketBase> List();
    }
}