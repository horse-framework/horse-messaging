using System.Collections.Generic;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Clients
{
    /// <summary>
    /// Manages clients in messaging server
    /// </summary>
    public class ClientRider
    {
        #region Properties

        private readonly SafeList<MessagingClient> _clients = new SafeList<MessagingClient>(2048);

        /// <summary>
        /// Client connect and disconnect operations
        /// </summary>
        public ArrayContainer<IClientHandler> Handlers { get; } = new ArrayContainer<IClientHandler>();

        /// <summary>
        /// Client authenticator implementations.
        /// If null, all clients will be accepted.
        /// </summary>
        public ArrayContainer<IClientAuthenticator> Authenticators { get; } = new ArrayContainer<IClientAuthenticator>();

        /// <summary>
        /// Authorization implementations for client operations
        /// </summary>
        public ArrayContainer<IClientAuthorization> Authorizations { get; } = new ArrayContainer<IClientAuthorization>();

        /// <summary>
        /// Authorization implementations for administration operations
        /// </summary>
        public ArrayContainer<IAdminAuthorization> AdminAuthorizations { get; } = new ArrayContainer<IAdminAuthorization>();

        /// <summary>
        /// All connected clients in the server
        /// </summary>
        public IEnumerable<MessagingClient> Clients => _clients.GetAsClone();

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
            _clients.Add(client);
            ConnectEvent.Trigger(client);
        }

        /// <summary>
        /// Removes the client from the server
        /// </summary>
        internal void Remove(MessagingClient client)
        {
            _clients.Remove(client);
            client.UnsubscribeFromAllQueues();
            DisconnectEvent.Trigger(client);
        }

        /// <summary>
        /// Finds client from unique id
        /// </summary>
        public MessagingClient Find(string uniqueId)
        {
            return _clients.Find(x => x.UniqueId == uniqueId && x.IsConnected);
        }

        /// <summary>
        /// Finds all connected client with specified name
        /// </summary>
        public List<MessagingClient> FindClientByName(string name)
        {
            return _clients.FindAll(x => x.Name == name && x.IsConnected);
        }

        /// <summary>
        /// Finds all connected client with specified type
        /// </summary>
        public List<MessagingClient> FindByType(string type)
        {
            return _clients.FindAll(x => x.Type == type && x.IsConnected);
        }

        /// <summary>
        /// Returns online clients
        /// </summary>
        public int GetOnlineClients()
        {
            return _clients.Count;
        }

        #endregion
    }
}