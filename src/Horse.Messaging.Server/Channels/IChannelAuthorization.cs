using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    public interface IChannelAuthorization
    {
        Task<bool> CanPush(HorseChannel channel, MessagingClient client, HorseMessage message);
        Task<bool> CanSubscribe(HorseChannel channel, MessagingClient client);
    }
}