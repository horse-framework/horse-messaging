using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    public interface IChannelAuthenticator
    {
        Task<bool> Authenticate(HorseChannel channel, MessagingClient client);
    }
}