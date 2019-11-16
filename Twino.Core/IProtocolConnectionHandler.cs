using System.Threading.Tasks;

namespace Twino.Core
{
    public interface IProtocolConnectionHandler<in TMessage>
    {
        Task<SocketBase> Connected(IConnectionInfo connection);

        Task Received(TMessage message);

        Task Disconnected();
    }
}