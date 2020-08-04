using System.IO;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Implementation for sending messages to Twino MQ
    /// </summary>
    public interface ITwinoBus : ITwinoQueueBus, ITwinoRouteBus, ITwinoDirectBus
    {
    }
}