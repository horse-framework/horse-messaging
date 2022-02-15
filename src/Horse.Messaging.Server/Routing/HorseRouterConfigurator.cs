using System;
using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Horse router configurator
    /// </summary>
    public class HorseRouterConfigurator
    {
        /// <summary>
        /// Router message event handlers
        /// </summary>
        public ArrayContainer<IRouterMessageHandler> MessageHandlers => Rider.Router.MessageHandlers;

        /// <summary>
        /// Horse rider
        /// </summary>
        public HorseRider Rider { get; }

        internal HorseRouterConfigurator(HorseRider rider)
        {
            Rider = rider;
        }

        /// <summary>
        /// Saves rotuer and bindings to the disk.
        /// All saved routers are reloaded when server restarted
        /// </summary>
        public void UsePersistentRouters(string configurationFilename = "routers.json")
        {
            Rider.Router.RouterConfigurationFilename = configurationFilename;
            Rider.Router.PersistentRouters = true;
        }
    }
}