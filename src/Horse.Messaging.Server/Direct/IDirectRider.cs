using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Direct
{
    /// <summary>
    /// Manages direct messages
    /// </summary>
    public interface IDirectRider
    {
        /// <summary>
        /// Direct message event handlers
        /// </summary>
        ArrayContainer<IDirectMessageHandler> MessageHandlers { get; }
    }
}