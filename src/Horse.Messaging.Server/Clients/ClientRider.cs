using System.Collections.Generic;
using Horse.Messaging.Protocol;
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
        /// Triggered when a client is connected 
        /// </summary>
        public ClientEventManager OnClientConnected { get; }

        /// <summary>
        /// Triggered when a client is disconnected 
        /// </summary>
        public ClientEventManager OnClientDisconnected { get; }

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

        #endregion

        /// <summary>
        /// Creates new client rider
        /// </summary>
        internal ClientRider(HorseRider rider)
        {
            Rider = rider;
            OnClientConnected = new ClientEventManager(EventNames.ClientConnected, rider);
            OnClientDisconnected = new ClientEventManager(EventNames.ClientDisconnected, rider);
        }

        #region Actions

        /// <summary>
        /// Adds new client to the server
        /// </summary>
        internal void AddClient(MessagingClient client)
        {
            _clients.Add(client);
            OnClientConnected.Trigger(client);
        }

        /// <summary>
        /// Removes the client from the server
        /// </summary>
        internal void RemoveClient(MessagingClient client)
        {
            _clients.Remove(client);
            client.UnsubscribeFromAllQueues();
            OnClientDisconnected.Trigger(client);
        }

        /// <summary>
        /// Finds client from unique id
        /// </summary>
        public MessagingClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds all connected client with specified name
        /// </summary>
        public List<MessagingClient> FindClientByName(string name)
        {
            return _clients.FindAll(x => x.Name == name);
        }

        /// <summary>
        /// Finds all connected client with specified type
        /// </summary>
        public List<MessagingClient> FindClientByType(string type)
        {
            return _clients.FindAll(x => x.Type == type);
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