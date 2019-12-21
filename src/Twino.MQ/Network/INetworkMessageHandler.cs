using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    public interface INetworkMessageHandler
    {
        Task Handle(MqClient client, TmqMessage message);
    }
}