using Twino.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    public class TmqSingleMessageConnector : SingleMessageConnector<TmqClient, TmqMessage>
    {
    }
}