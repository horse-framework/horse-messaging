using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolConnectionHandler<in TMessage>
    {
        Task<SocketBase> Connected(IConnectionInfo connection);

        Task Received(TMessage message);

        Task Disconnected();
    }
}