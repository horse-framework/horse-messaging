using System.Threading.Tasks;
using Twino.MQ.Channels;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    public interface IQueueAuthenticator
    {
        Task<bool> Authenticate(ChannelQueue channel, MqClient client, ClientInformation information);
    }
}