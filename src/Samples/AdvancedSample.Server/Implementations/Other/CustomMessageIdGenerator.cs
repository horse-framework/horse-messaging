using Horse.Messaging.Protocol;

namespace AdvancedSample.Server.Implementations.Other
{
    public class CustomMessageIdGenerator : IUniqueIdGenerator
    {
       
        public string Create()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
