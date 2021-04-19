using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    public interface IChannelEventHandler
    {
        Task OnCreated(HorseChannel channel);
        Task OnRemoved(HorseChannel channel);
        Task OnSubscribe(HorseChannel channel, MessagingClient client);
        Task OnUnsubscribe(HorseChannel channel, MessagingClient client);
        
    }
}