using Twino.Core.Protocols;

namespace Twino.Core
{
    public interface ITwinoServer
    {
        IProtocolConnectionHandler<TMessage> UseProtocol<TMessage>(ITwinoProtocol<TMessage> protocol);
    }
}