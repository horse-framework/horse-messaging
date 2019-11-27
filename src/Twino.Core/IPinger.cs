namespace Twino.Core
{
    /// <summary>
    /// Manages ping and pong messages for connected piped clients
    /// </summary>
    public interface IPinger
    {
        /// <summary>
        /// Add new client to pinger
        /// </summary>
        void Add(SocketBase socket);

        /// <summary>
        /// Remove a client from pinger
        /// </summary>
        void Remove(SocketBase socket);

        /// <summary>
        /// Starts to ping connected clients
        /// </summary>
        void Start();

        /// <summary>
        /// Stops ping / pong operation and releases all resources
        /// </summary>
        void Stop();
    }
}