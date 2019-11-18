using System.Collections.Generic;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolConnectionHandler<in TMessage>
    {
        Task<ServerSocketBase> Connected(ITwinoServer server, IConnectionInfo connection, Dictionary<string, string> properties);

        Task Received(ITwinoServer server, IConnectionInfo info, TMessage message);

        Task Disconnected(ITwinoServer server, IConnectionInfo info, ServerSocketBase client);
    }
}