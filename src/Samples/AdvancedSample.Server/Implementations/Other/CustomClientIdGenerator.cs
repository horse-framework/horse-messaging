using Horse.Messaging.Protocol;

namespace AdvancedSample.Server.Implementations.Other
{
    public class CustomClientIdGenerator : IUniqueIdGenerator
    {
        private static int counter = 0;
       
        public string Create()
        {
            return Interlocked.Increment(ref counter).ToString();
        }
    }
}
