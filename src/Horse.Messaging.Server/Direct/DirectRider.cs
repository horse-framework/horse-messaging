using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Direct
{
    /// <summary>
    /// Manages direct messages
    /// </summary>
    public class DirectRider
    {
        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Direct message event handlers
        /// </summary>
        public ArrayContainer<IDirectMessageHandler> MessageHandlers { get; } = new ArrayContainer<IDirectMessageHandler>();

        /// <summary>
        /// Creates new direct rider
        /// </summary>
        internal DirectRider(HorseRider rider)
        {
            Rider = rider;
        }
    }
}