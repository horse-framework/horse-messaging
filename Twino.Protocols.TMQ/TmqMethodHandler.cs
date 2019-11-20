using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Message received handler
    /// </summary>
    public delegate Task TmqMessageHandler(TmqServerSocket socket, TmqMessage message);

    public class TmqMethodHandler : IProtocolConnectionHandler<TmqMessage>
    {
        /// <summary>
        /// Default unique Id generator
        /// </summary>
        private readonly IUniqueIdGenerator _uniqueIdGenerator = new DefaultUniqueIdGenerator();

        /// <summary>
        /// User defined message received handler
        /// </summary>
        private readonly TmqMessageHandler _action;

        public TmqMethodHandler(TmqMessageHandler action)
        {
            _action = action;
        }

        /// <summary>
        /// Triggered when a TMQ client is connected. 
        /// </summary>
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new TmqServerSocket(server, connection, _uniqueIdGenerator));
        }

        /// <summary>
        /// Triggered when a TMQ message received from client
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            await _action((TmqServerSocket) client, message);
        }

        /// <summary>
        /// Triggered when a TMQ client is connected. 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}