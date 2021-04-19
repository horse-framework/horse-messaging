using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Manages queues in messaging server
    /// </summary>
    public interface IQueueRider
    {
        /// <summary>
        /// Queue event handlers
        /// </summary>
        ArrayContainer<IQueueEventHandler> EventHandlers { get; }

        /// <summary>
        /// Queue authenticators
        /// </summary>
        ArrayContainer<IQueueAuthenticator> Authenticators { get; }

        /// <summary>
        /// Event handlers to track queue message events
        /// </summary>
        ArrayContainer<IQueueMessageEventHandler> MessageHandlers { get; }

        /// <summary>
        /// Default queue options
        /// </summary>
        QueueOptions Options { get; }
    }
}