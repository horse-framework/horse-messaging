using System.Collections.Concurrent;

namespace Horse.Messaging.Server.Stores;

public class StoreRider
{
    public StoreOptions DefaultOptions { get; } = new StoreOptions();
    private ConcurrentDictionary<string, HorseStore> _stores = new();
}