using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Manages routers in messaging server
    /// </summary>
    public interface IRouterRider
    {
        /// <summary>
        /// Router message event handlers
        /// </summary>
        ArrayContainer<IRouterMessageHandler> MessageHandlers { get; }
    }
}