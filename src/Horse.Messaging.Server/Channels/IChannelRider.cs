using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Manages channels in messaging server
    /// </summary>
    public interface IChannelRider
    {
        /// <summary>
        /// Default channel options
        /// </summary>
        HorseChannelOptions Options { get; }

        /// <summary>
        /// Event handlers to track channel events
        /// </summary>
        ArrayContainer<IChannelEventHandler> EventHandlers { get; }

        /// <summary>
        /// Channel authenticators
        /// </summary>
        ArrayContainer<IChannelAuthorization> Authenticators { get; }
    }
}