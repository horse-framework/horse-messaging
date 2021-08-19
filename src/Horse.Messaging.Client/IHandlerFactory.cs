using System;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Handler creator factory.
    /// Used for external handler injection from an external source
    /// </summary>
    public interface IHandlerFactory
    {
        /// <summary>
        /// Creates new consumer instance
        /// </summary>
        /// <param name="consumerType">Type of the consumer</param>
        /// <returns>Consumer instance</returns>
        ProvidedHandler CreateHandler(Type consumerType);
        
        /// <summary>
        /// Creates new interceptor instance
        /// </summary>
        /// <param name="interceptorType">Type of the interceptor</param>
        /// <returns>Interceptor instance</returns>
        IHorseInterceptor CreateInterceptor(Type interceptorType);
    }
    
}