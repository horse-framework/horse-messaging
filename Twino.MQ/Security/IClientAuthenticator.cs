using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    public interface IClientAuthenticator
    {
        Task<bool> Authenticate(MQServer server, MqClient client, ClientInformation information);
    }
}