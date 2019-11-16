using System.Collections.Generic;

namespace Twino.Server.WebSockets
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
        void Add(ServerSocket client);

        /// <summary>
        /// Removes the client from the online clients container
        /// </summary>
        void Remove(ServerSocket client);

        /// <summary>
        /// Gets all online clients
        /// </summary>
        IEnumerable<ServerSocket> List();
    }
}