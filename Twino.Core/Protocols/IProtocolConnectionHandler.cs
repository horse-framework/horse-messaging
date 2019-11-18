using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolConnectionHandler<in TMessage>
    {
        Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection);

        Task Received(ITwinoServer server, TMessage message);

        Task Disconnected(ITwinoServer server, SocketBase client);
    }
}