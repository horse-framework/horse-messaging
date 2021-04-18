using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Cache
{
    public interface IHorseCache
    {
        Task<TData> Get<TData>(string key);

        Task<HorseResult> Set<TData>(string key, TData data);

        Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration);

        Task<HorseResult> Remove(string key);

        Task<HorseResult> Purge();
    }
}