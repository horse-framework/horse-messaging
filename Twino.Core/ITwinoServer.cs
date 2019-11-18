using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Core
{
    public interface ITwinoServer
    {
        ILogger Logger { get; }

        IPinger Pinger { get; }

        void UseProtocol<TMessage>(ITwinoProtocol<TMessage> protocol);

        Task SwitchProtocol(IConnectionInfo info, string newProtocolName, Dictionary<string, string> properties);

        ITwinoProtocol FindProtocol(string name);
    }
}