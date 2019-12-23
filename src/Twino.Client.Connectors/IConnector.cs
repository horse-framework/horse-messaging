using Twino.Core;

namespace Twino.Client.Connectors
{
    /// <summary>
    /// Connector interface for managing specified case connection types.
    /// </summary>
    public interface IConnector<out TClient, TMessage>
        where TClient : ClientSocketBase<TMessage>, new()
    {
        /// <summary>
        /// If true, connector is running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// If true, connector is connected to specified host
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Adds a host to remote hosts list
        /// </summary>
        void AddHost(string host);

        /// <summary>
        /// Removes the host from remote hosts list
        /// </summary>
        void RemoveHost(string host);

        /// <summary>
        /// Clear all hosts in remote hosts list
        /// </summary>
        void ClearHosts();

        /// <summary>
        /// Add a new custom property.
        /// If the property is already exists, it will be changed.
        /// </summary>
        void AddProperty(string key, string value);

        /// <summary>
        /// Removes custom the property
        /// </summary>
        void RemoveProperty(string key);

        /// <summary>
        /// Clears all custom properties
        /// </summary>
        void ClearProperties();

        /// <summary>
        /// Gets the current client socket that connected to the host
        /// </summary>
        /// <returns></returns>
        TClient GetClient();

        /// <summary>
        /// Runs the connector
        /// </summary>
        void Run();

        /// <summary>
        /// Stops the connector
        /// </summary>
        void Abort();

        /// <summary>
        /// Sends the message to the server
        /// </summary>
        bool Send(byte[] data);
    }
}