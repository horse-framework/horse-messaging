using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    public interface IChannelEventHandler
    {
        Task OnSusbcribe(HorseChannel channel, MessagingClient client);
        Task OnUnsusbcribe(HorseChannel channel, MessagingClient client);
    }
}