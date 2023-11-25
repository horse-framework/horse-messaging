using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Routing;

namespace HostedServiceSample.Server.CustomBindings;

public class SampleDirectBinding: DirectBinding
{
    public override Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        var userIdParam = message.FindHeader("UserId");
        userIdParam = string.IsNullOrEmpty(userIdParam) ? "0" : userIdParam;
        var messageOwner = int.Parse(userIdParam);
        var hasDebugClient = GetClients().Any(m => (int.Parse(m.Name) == messageOwner));
        SetClientFilter(hasDebugClient ? m => int.Parse(m.Name) == messageOwner : m => m.Name == "0");
        return base.Send(sender, message);
    }
}