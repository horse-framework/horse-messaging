using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Horse cache configurator
    /// </summary>
    public class HorseCacheConfigurator
    {
        /// <summary>
        /// Cache authorizations
        /// </summary>
        public ArrayContainer<ICacheAuthorization> Authorizations => _rider.Cache.Authorizations;

        /// <summary>
        /// Options for cache
        /// </summary>
        public HorseCacheOptions Options => _rider.Cache.Options;

        private readonly HorseRider _rider;

        internal HorseCacheConfigurator(HorseRider rider)
        {
            _rider = rider;
        }
    }
}