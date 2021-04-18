using System.IO;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Cache authorization implementation
    /// </summary>
    public interface ICacheAuthorization
    {
        /// <summary>
        /// Returns true if client has permission to get the key value
        /// </summary>
        bool CanGet(MessagingClient client, string key);

        /// <summary>
        /// Returns true if client has permission to add a new key
        /// </summary>
        bool CanSet(MessagingClient client, string key, MemoryStream value);

        /// <summary>
        /// Returns true if client has permission to remove key
        /// </summary>
        bool CanRemove(MessagingClient client, string key);

        /// <summary>
        /// Return true if client has permission to purge all keys
        /// </summary>
        bool CanPurge(MessagingClient client);
    }
}