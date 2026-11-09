using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Clients;

/// <summary>
/// Manages clients in messaging server
/// </summary>
public class ClientRider
{
    #region Client Bucket

    /// <summary>
    /// Connected clients with the same name or the same type, in connection order.
    /// Connect and disconnect change the bucket in O(1). The array for readers is built on the first read after a change,
    /// so a bucket nobody reads, such as "(Unknown)" shared by every client without a name or type, is never copied.
    /// </summary>
    private sealed class ClientBucket
    {
        private readonly LinkedList<MessagingClient> _clients = new();
        private readonly Dictionary<MessagingClient, LinkedListNode<MessagingClient>> _nodes = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Array for readers, null after a change until the next read
        /// </summary>
        private volatile MessagingClient[] _snapshot = [];

        public void Add(MessagingClient client)
        {
            lock (_clients)
            {
                if (_nodes.ContainsKey(client))
                    return;

                _nodes.Add(client, _clients.AddLast(client));
                _snapshot = null;
            }
        }

        /// <summary>
        /// Removes the client, returns true if the bucket is empty
        /// </summary>
        public bool Remove(MessagingClient client)
        {
            lock (_clients)
            {
                if (_nodes.Remove(client, out LinkedListNode<MessagingClient> node))
                {
                    _clients.Remove(node);
                    _snapshot = null;
                }

                return _clients.Count == 0;
            }
        }

        public MessagingClient[] GetClients()
        {
            MessagingClient[] snapshot = _snapshot;
            if (snapshot != null)
                return snapshot;

            lock (_clients)
            {
                snapshot = _snapshot;
                if (snapshot != null)
                    return snapshot;

                if (_clients.Count == 0)
                    snapshot = [];
                else
                {
                    snapshot = new MessagingClient[_clients.Count];
                    _clients.CopyTo(snapshot, 0);
                }

                _snapshot = snapshot;
                return snapshot;
            }
        }
    }

    #endregion

    #region Properties

    private readonly object _clientsByNameLock = new();
    private readonly object _clientsByTypeLock = new();

    private readonly ConcurrentDictionary<string, MessagingClient> _clientsById = new();
    private readonly ConcurrentDictionary<string, ClientBucket> _clientsByName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ClientBucket> _clientsByType = new(StringComparer.Ordinal);

    //name and type are looked up from a part of the message target without creating a string
    private readonly ConcurrentDictionary<string, ClientBucket>.AlternateLookup<ReadOnlySpan<char>> _clientsByNameLookup;
    private readonly ConcurrentDictionary<string, ClientBucket>.AlternateLookup<ReadOnlySpan<char>> _clientsByTypeLookup;

    /// <summary>
    /// Count of the clients in _clientsById. ConcurrentDictionary.Count takes all locks of the dictionary.
    /// </summary>
    private int _onlineClients;

    /// <summary>
    /// Client connect and disconnect operations
    /// </summary>
    public ArrayContainer<IClientHandler> Handlers { get; } = new();

    /// <summary>
    /// Client authenticator implementations.
    /// If null, all clients will be accepted.
    /// </summary>
    public ArrayContainer<IClientAuthenticator> Authenticators { get; } = new();

    /// <summary>
    /// Authorization implementations for client operations
    /// </summary>
    public ArrayContainer<IClientAuthorization> Authorizations { get; } = new();

    /// <summary>
    /// Authorization implementations for administration operations
    /// </summary>
    public ArrayContainer<IAdminAuthorization> AdminAuthorizations { get; } = new();

    /// <summary>
    /// All connected clients in the server.
    /// It's not a snapshot: clients connecting or disconnecting during the enumeration may or may not be included.
    /// </summary>
    public IEnumerable<MessagingClient> Clients => EnumerateClients();

    /// <summary>
    /// Id generator for clients which has no specified unique id
    /// </summary>
    public IUniqueIdGenerator ClientIdGenerator { get; internal set; } = new DefaultUniqueIdGenerator();

    /// <summary>
    /// Root horse rider object
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Event Manager for HorseEventType.ClientConnect
    /// </summary>
    public EventManager ConnectEvent { get; }

    /// <summary>
    /// Event Manager for HorseEventType.ClientDisconnect
    /// </summary>
    public EventManager DisconnectEvent { get; }

    #endregion

    /// <summary>
    /// Creates new client rider
    /// </summary>
    internal ClientRider(HorseRider rider)
    {
        Rider = rider;
        ConnectEvent = new EventManager(Rider, HorseEventType.ClientConnect);
        DisconnectEvent = new EventManager(Rider, HorseEventType.ClientDisconnect);
        _clientsByNameLookup = _clientsByName.GetAlternateLookup<ReadOnlySpan<char>>();
        _clientsByTypeLookup = _clientsByType.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    #region Actions

    /// <summary>
    /// Adds new client to the server.
    /// Returns false if another client with the same unique id is online.
    /// </summary>
    internal bool Add(MessagingClient client)
    {
        //the unique id is checked while connecting, but two connections with the same id
        //may pass that check together while they are authenticated
        if (!_clientsById.TryAdd(client.UniqueId, client))
            return false;

        Interlocked.Increment(ref _onlineClients);

        AddToBucket(_clientsByName, _clientsByNameLock, client.Name, client);
        AddToBucket(_clientsByType, _clientsByTypeLock, client.Type, client);

        ConnectEvent.Trigger(client);
        return true;
    }

    /// <summary>
    /// Removes the client from the server
    /// </summary>
    internal void Remove(MessagingClient client)
    {
        //removes only this client, the unique id may belong to another connection
        if (client.UniqueId != null && _clientsById.TryRemove(new KeyValuePair<string, MessagingClient>(client.UniqueId, client)))
            Interlocked.Decrement(ref _onlineClients);

        RemoveFromBucket(_clientsByName, _clientsByNameLock, client.Name, client);
        RemoveFromBucket(_clientsByType, _clientsByTypeLock, client.Type, client);

        //events first: it runs no user code. Queue and channel removal call user event handlers,
        //one that throws synchronously must not leave the client in event subscriptions (no periodic cleanup exists)
        client.UnsubscribeFromAllEvents();
        client.UnsubscribeFromAllQueues();
        client.UnsubscribeFromAllChannels();
        DisconnectEvent.Trigger(client);
    }

    private static void AddToBucket(ConcurrentDictionary<string, ClientBucket> buckets, object writeLock, string key, MessagingClient client)
    {
        if (key == null)
            return;

        lock (writeLock)
        {
            if (!buckets.TryGetValue(key, out ClientBucket bucket))
            {
                bucket = new ClientBucket();
                buckets[key] = bucket;
            }

            bucket.Add(client);
        }
    }

    private static void RemoveFromBucket(ConcurrentDictionary<string, ClientBucket> buckets, object writeLock, string key, MessagingClient client)
    {
        if (key == null)
            return;

        lock (writeLock)
        {
            //empty buckets are removed, unique names would grow the dictionary forever
            if (buckets.TryGetValue(key, out ClientBucket bucket) && bucket.Remove(client))
                buckets.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Removes all connected clients
    /// </summary>
    internal void DisconnectAllClients()
    {
        foreach (KeyValuePair<string, MessagingClient> pair in _clientsById)
        {
            //removed here instead of Clear, the count stays right when Remove is called for the client later
            if (_clientsById.TryRemove(pair))
                Interlocked.Decrement(ref _onlineClients);

            MessagingClient client = pair.Value;
            client.UnsubscribeFromAllEvents();
            client.UnsubscribeFromAllQueues();
            client.UnsubscribeFromAllChannels();
            client.Disconnect();
        }

        lock (_clientsByNameLock)
            _clientsByName.Clear();

        lock (_clientsByTypeLock)
            _clientsByType.Clear();
    }

    /// <summary>
    /// Finds client from unique id.
    /// Leading and trailing white spaces of the unique id are ignored.
    /// </summary>
    public MessagingClient Find(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId))
            return null;

        _clientsById.TryGetValue(uniqueId.Trim(), out MessagingClient client);
        return client;
    }

    /// <summary>
    /// Finds all connected client with specified name, in connection order.
    /// Leading and trailing white spaces of the name are ignored.
    /// Returns an empty array if there is no client with the name.
    /// The returned array must not be modified.
    /// </summary>
    public MessagingClient[] FindClientByName(string name)
    {
        return name == null ? [] : FindClientByName(name.AsSpan());
    }

    /// <summary>
    /// Finds all connected client with specified name, in connection order.
    /// Leading and trailing white spaces of the name are ignored.
    /// The returned array must not be modified.
    /// </summary>
    internal MessagingClient[] FindClientByName(ReadOnlySpan<char> name)
    {
        name = name.Trim();
        if (name.IsEmpty)
            return [];

        return _clientsByNameLookup.TryGetValue(name, out ClientBucket bucket) ? bucket.GetClients() : [];
    }

    /// <summary>
    /// Finds all connected client with specified type, in connection order.
    /// Leading and trailing white spaces of the type are ignored.
    /// Returns an empty array if there is no client with the type.
    /// The returned array must not be modified.
    /// </summary>
    public MessagingClient[] FindByType(string type)
    {
        return type == null ? [] : FindByType(type.AsSpan());
    }

    /// <summary>
    /// Finds all connected client with specified type, in connection order.
    /// Leading and trailing white spaces of the type are ignored.
    /// The returned array must not be modified.
    /// </summary>
    internal MessagingClient[] FindByType(ReadOnlySpan<char> type)
    {
        type = type.Trim();
        if (type.IsEmpty)
            return [];

        return _clientsByTypeLookup.TryGetValue(type, out ClientBucket bucket) ? bucket.GetClients() : [];
    }

    /// <summary>
    /// Returns online clients
    /// </summary>
    public int GetOnlineClients()
    {
        return Volatile.Read(ref _onlineClients);
    }

    /// <summary>
    /// Enumerating the dictionary takes no lock. ConcurrentDictionary.Values takes all locks and copies the values.
    /// </summary>
    private IEnumerable<MessagingClient> EnumerateClients()
    {
        foreach (KeyValuePair<string, MessagingClient> pair in _clientsById)
            yield return pair.Value;
    }

    #endregion
}
