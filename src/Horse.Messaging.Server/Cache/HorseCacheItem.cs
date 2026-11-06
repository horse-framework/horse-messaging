using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Cache Item
/// </summary>
public class HorseCacheItem
{
    private string _key;
    private DateTime _expiration;
    private DateTime? _expirationWarning;
    private string[] _tags;
    internal sealed class WarningCounter
    {
        internal int Count;
    }

    private WarningCounter _warningCounter;
    internal WarningCounter WarningState
    {
        get
        {
            WarningCounter current = Volatile.Read(ref _warningCounter);
            if (current != null)
                return current;
            WarningCounter created = new();
            return Interlocked.CompareExchange(ref _warningCounter, created, null) ?? created;
        }
    }
    internal string CacheKey;
    internal string[] IndexedTags = Array.Empty<string>();
    internal string[] TagValues => _tags ?? Array.Empty<string>();
    internal Action<HorseCacheItem> MetadataChanged;
    internal object MetadataGate;

    internal string TagHeader { get; private set; }
    internal string ExpirationHeader { get; private set; }
    internal string WarningHeader { get; private set; }

    /// <summary>
    /// Cache key. Once stored, use Set to create an entry under a different key.
    /// </summary>
    public string Key
    {
        get => _key;
        set
        {
            object gate = MetadataGate;
            if (gate == null)
                SetKey(value);
            else
                lock (gate) SetKey(value);
        }
    }

    private void SetKey(string value)
    {
        if (CacheKey != null && !StringComparer.OrdinalIgnoreCase.Equals(CacheKey, value))
            throw new InvalidOperationException("Use Set to create an entry under a different cache key.");
        _key = value;
    }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime Expiration
    {
        get => _expiration;
        set
        {
            object gate = MetadataGate;
            if (gate == null)
                SetExpiration(value);
            else
                lock (gate) SetExpiration(value);
        }
    }

    private void SetExpiration(DateTime value)
    {
        _expiration = value;
        ExpirationHeader = value.ToUnixSeconds().ToString(CultureInfo.InvariantCulture);
        MetadataChanged?.Invoke(this);
    }

    /// <summary>
    /// The date for warning of key expiration.
    /// After that time, requester will receive extra information that tells the cache is about to expire but not expired yet. 
    /// </summary>
    public DateTime? ExpirationWarning
    {
        get => _expirationWarning;
        set
        {
            _expirationWarning = value;
            WarningHeader = value?.ToUnixSeconds().ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// How many times requesters are warned.
    /// </summary>
    public int ExpirationWarnCount
    {
        get
        {
            WarningCounter current = Volatile.Read(ref _warningCounter);
            return current == null ? 0 : Volatile.Read(ref current.Count);
        }
        set
        {
            if (value != 0 || Volatile.Read(ref _warningCounter) != null)
                Volatile.Write(ref WarningState.Count, value);
        }
    }

    /// <summary>
    /// Cache key tags.
    /// Cache keys can be removed by their tags.
    /// The getter returns a copy; assign the property to update tags and indexes.
    /// </summary>
    public string[] Tags
    {
        get => _tags is { Length: > 0 } ? (string[])_tags.Clone() : Array.Empty<string>();
        set
        {
            object gate = MetadataGate;
            if (gate == null)
                SetTags(value);
            else
                lock (gate) SetTags(value);
        }
    }

    private void SetTags(string[] value)
    {
        _tags = value is { Length: > 0 } ? (string[])value.Clone() : Array.Empty<string>();
        TagHeader = _tags.Length > 0 ? string.Join(',', _tags) : null;
        MetadataChanged?.Invoke(this);
    }

    /// <summary>
    /// If true, cache will be stored in persistent storage.
    /// </summary>
    public bool IsPersistent { get; set; }

    /// <summary>
    /// Cache value. Values created by the cache are read-only streams over an
    /// owned buffer; use Set to replace an entry's payload.
    /// </summary>
    public MemoryStream Value { get; set; }

    internal HorseCacheItem WithValue(MemoryStream value)
    {
        // Value-only versions share the warning election, including a GET that
        // captured the previous version before the increment was published.
        WarningCounter state = WarningState;
        HorseCacheItem copy = (HorseCacheItem)MemberwiseClone();
        copy._warningCounter = state;
        copy.Value = value;
        return copy;
    }
}
