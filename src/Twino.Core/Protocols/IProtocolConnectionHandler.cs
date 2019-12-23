using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    /// <summary>
    /// Twino Protocol Connection handler implementation
    /// </summary>
    public interface IProtocolConnectionHandler<TClient, in TMessage>
        where TClient : SocketBase
    {
        /// <summary>
        /// Triggered when a piped client is connected. 
        /// </summary>
        Task<TClient> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data);

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        Task Ready(ITwinoServer server, TClient client);

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        Task Received(ITwinoServer server, IConnectionInfo info, TClient client, TMessage message);

        /// <summary>
        /// Triggered when a piped client is disconnected. 
        /// </summary>
        Task Disconnected(ITwinoServer server, TClient client);
    }
}