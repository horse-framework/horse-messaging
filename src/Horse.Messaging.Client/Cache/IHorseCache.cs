using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Cache;

/// <inheritdoc />
public interface IHorseCache<TIdentifier> : IHorseCache
{
}

/// <summary>
/// Cache management implementation for client
/// </summary>
public interface IHorseCache
{
    // ── Get ──

    Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken);
    Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken);
    Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken);

    // ── GetIncrementalValue ──

    Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken);
    Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken);
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken);
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken);

    // ── List ──

    Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken);
    Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken);

    // ── Set<TData> ──

    Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken);
    Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken);
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    // ── SetString ──

    Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken);
    Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken);
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    // ── SetData ──

    Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken);
    Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken);
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    // ── Remove / Purge ──

    Task<HorseResult> Remove(string key, CancellationToken cancellationToken);
    Task<HorseResult> Purge(CancellationToken cancellationToken);
    Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken);
}