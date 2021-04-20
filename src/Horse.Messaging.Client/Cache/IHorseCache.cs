using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Cache
{
    /// <summary>
    /// Cache management implementation for client
    /// </summary>
    public interface IHorseCache
    {
        /// <summary>
        /// Gets an item from cache
        /// </summary>
        /// <param name="key">Cache key</param>
        Task<TData> Get<TData>(string key);

        /// <summary>
        /// Sets an item to cache store
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="data">Cache item</param>
        Task<HorseResult> Set<TData>(string key, TData data);

        /// <summary>
        /// Sets an item to cache store with specified duration
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="data">Cache item</param>
        /// <param name="duration">Cache expiration duration</param>
        Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration);

        /// <summary>
        /// Removes a key and value from from
        /// </summary>
        /// <param name="key">Cache key</param>
        Task<HorseResult> Remove(string key);

        /// <summary>
        /// Removes all cache key and values
        /// </summary>
        Task<HorseResult> Purge();
    }
}