using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    public delegate Task TmqMessageHandler(TmqServerSocket socket, TmqMessage message);

    public class TmqMethodHandler : IProtocolConnectionHandler<TmqMessage>
    {
        private readonly IUniqueIdGenerator _uniqueIdGenerator = new DefaultUniqueIdGenerator();
        private readonly TmqMessageHandler _action;

        public TmqMethodHandler(TmqMessageHandler action)
        {
            _action = action;
        }

        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new TmqServerSocket(server, connection, _uniqueIdGenerator));
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TmqMessage message)
        {
            await _action((TmqServerSocket) client, message);
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}