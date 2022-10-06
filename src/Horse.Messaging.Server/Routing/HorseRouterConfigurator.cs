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
        /// Uses custom queue persistent configurator.
        /// By default, queue configurations are saved to json file.
        /// Setting this value null, persistent configurations will be disabled. 
        /// </summary>
        public HorseRouterConfigurator UseCustomPersistentConfigurator(IOptionsConfigurator<RouterConfiguration> configurator)
        {
            Rider.Router.OptionsConfigurator = configurator;
            return this;
        }
    }
}