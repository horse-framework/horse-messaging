using System.Threading.Tasks;
using Twino.MQ.Channels;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    public interface IChannelAuthenticator
    {
        Task<bool> Authenticate(Channel channel, MqClient client, ClientInformation information);
    }
}