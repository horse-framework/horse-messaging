using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    #region Properties

    private readonly object _clientsByNameLock = new();
    private readonly object _clientsByTypeLock = new();

    private readonly ConcurrentDictionary<string, MessagingClient> _clientsById = new();
    private readonly ConcurrentDictionary<string, MessagingClient[]> _clientsByName = new();
    private readonly ConcurrentDictionary<string, MessagingClient[]> _clientsByType = new();

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
    /// All connected clients in the server
    /// </summary>
    public IEnumerable<MessagingClient> Clients => _clientsById.Values;

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
    }

    #region Actions

    /// <summary>
    /// Adds new client to the server
    /// </summary>
    internal void Add(MessagingClient client)
    {
        _clientsById[client.UniqueId] = client;

        lock (_clientsByNameLock)
        {
            if (_clientsByName.TryGetValue(client.Name, out MessagingClient[] nameClients))
            {
                var list = nameClients.ToList();
                list.Add(client);
                _clientsByName[client.Name] = list.ToArray();
            }
            else
                _clientsByName[client.Name] = [client];
        }

        lock (_clientsByTypeLock)
        {
            if (_clientsByType.TryGetValue(client.Type, out MessagingClient[] typeClients))
            {
                var list = typeClients.ToList();
                list.Add(client);
                _clientsByType[client.Type] = list.ToArray();
            }
            else
                _clientsByType[client.Type] = [client];
        }

        ConnectEvent.Trigger(client);
    }

    /// <summary>
    /// Removes the client from the server
    /// </summary>
    internal void Remove(MessagingClient client)
    {
        _clientsById.TryRemove(client.UniqueId, out _);

        lock (_clientsByNameLock)
            if (_clientsByName.TryGetValue(client.Name, out MessagingClient[] nameClients))
                _clientsByName[client.Name] = nameClients.Where(x => x != client).ToArray();

        lock (_clientsByTypeLock)
            if (_clientsByType.TryGetValue(client.Type, out MessagingClient[] typeClients))
                _clientsByType[client.Type] = typeClients.Where(x => x != client).ToArray();

        client.UnsubscribeFromAllQueues();
        client.UnsubscribeFromAllChannels();
        DisconnectEvent.Trigger(client);
    }

    /// <summary>
    /// Removes all connected clients
    /// </summary>
    internal void DisconnectAllClients()
    {
        foreach (var pair in _clientsById)
        {
            MessagingClient client = pair.Value;
            client.UnsubscribeFromAllQueues();
            client.UnsubscribeFromAllChannels();
            client.Disconnect();
        }

        _clientsById.Clear();

        lock (_clientsByNameLock)
            _clientsByName.Clear();

        lock (_clientsByTypeLock)
            _clientsByType.Clear();
    }

    /// <summary>
    /// Finds client from unique id
    /// </summary>
    public MessagingClient Find(string uniqueId)
    {
        _clientsById.TryGetValue(uniqueId, out MessagingClient client);
        return client;
    }

    /// <summary>
    /// Finds all connected client with specified name
    /// </summary>
    public MessagingClient[] FindClientByName(string name)
    {
        _clientsByName.TryGetValue(name, out MessagingClient[] clients);
        return clients;
    }

    /// <summary>
    /// Finds all connected client with specified type
    /// </summary>
    public MessagingClient[] FindByType(string type)
    {
        _clientsByType.TryGetValue(type, out MessagingClient[] clients);
        return clients;
    }

    /// <summary>
    /// Returns online clients
    /// </summary>
    public int GetOnlineClients()
    {
        return _clientsById.Count;
    }

    #endregion
}