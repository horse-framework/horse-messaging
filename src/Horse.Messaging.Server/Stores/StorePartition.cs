using System;
using System.Collections.Concurrent;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Stores;

public class StorePartition
{
    private StoreFile[] _files = [];
    private ConcurrentDictionary<string, StoreSubscription> _subscriptions = new();
    public HorseStore Store { get; }

    public StorePartition(HorseStore store)
    {
        Store = store;
    }

    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void Subscribe(MessagingClient client)
    {
        throw new NotImplementedException();
    }
}