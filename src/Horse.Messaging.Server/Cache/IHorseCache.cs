using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Horse cacha implementation
    /// </summary>
    public interface IHorseCache
    {
        /// <summary>
        /// Cache authorizations
        /// </summary>
        ArrayContainer<ICacheAuthorization> Authorizations { get; }
        
        /// <summary>
        /// Options for cache
        /// </summary>
        HorseCacheOptions Options { get; }
    }
}