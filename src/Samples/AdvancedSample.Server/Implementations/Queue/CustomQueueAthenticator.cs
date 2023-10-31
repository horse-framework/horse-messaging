using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Security;

namespace AdvancedSample.Server.Implementations.Queue
{
    public class CustomQueueAthenticator : IQueueAuthenticator
    {
        public async Task<bool> Authenticate(HorseQueue queue, MessagingClient client)
        {

            return true;
        }
    }
}
