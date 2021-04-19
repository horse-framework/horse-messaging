using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Clients
{
    /// <summary>
    /// Client management implementation
    /// </summary>
    public interface IClientRider
    {
        /// <summary>
        /// Client connect and disconnect operations
        /// </summary>
        public ArrayContainer<IClientHandler> Handlers { get; }

        /// <summary>
        /// Client authenticator implementations.
        /// If null, all clients will be accepted.
        /// </summary>
        public ArrayContainer<IClientAuthenticator> Authenticators { get; }

        /// <summary>
        /// Authorization implementations for client operations
        /// </summary>
        public ArrayContainer<IClientAuthorization> Authorizations { get; }

        /// <summary>
        /// Authorization implementations for administration operations
        /// </summary>
        public ArrayContainer<IAdminAuthorization> AdminAuthorizations { get; }
    }
}