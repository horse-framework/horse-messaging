using System;
using System.Threading.Tasks;

namespace Horse.Mq.Client
{
    /// <summary>
    /// Consumer creator factory.
    /// Used for external consumer injection from an external source
    /// </summary>
    public interface IConsumerFactory
    {
        /// <summary>
        /// Creates new consumer instance
        /// </summary>
        /// <param name="consumerType">Type of the consumer</param>
        /// <returns>Consumer instance</returns>
        Task<object> CreateConsumer(Type consumerType);

        /// <summary>
        /// Executed after consumer instance is created and message is consumed
        /// </summary>
        /// <param name="error">If not null, consume operation failed with an exception</param>
        void Consumed(Exception error);
    }
}