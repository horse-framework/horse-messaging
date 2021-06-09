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
        /// Create new scope for provider when consumer registered scoped lifetime.
        /// </summary>
        void CreateScope();
        /// <summary>
        /// Creates new consumer instance
        /// </summary>
        /// <param name="consumerType">Type of the consumer</param>
        /// <returns>Consumer instance</returns>
        Task<object> CreateConsumer(Type consumerType);
        
        /// <summary>
        /// Creates new interceptor instance
        /// </summary>
        /// <param name="interceptorType">Type of the interceptor</param>
        /// <returns>Interceptor instance</returns>
        Task<IHorseMessageInterceptor> CreateInterceptor(Type interceptorType);

        /// <summary>
        /// Executed after consumer instance is created and message is consumed
        /// </summary>
        /// <param name="error">If not null, consume operation failed with an exception</param>
        void Consumed(Exception error);
    }
}