using System;
using System.Threading.Tasks;

namespace Twino.Client.TMQ
{
    internal class NoConsumerFactory : IConsumerFactory
    {
        public Task<object> CreateConsumer(Type consumerType)
        {
            throw new NotSupportedException();
        }

        public void Consumed(Exception error)
        {
            throw new NotSupportedException();
        }
    }
}