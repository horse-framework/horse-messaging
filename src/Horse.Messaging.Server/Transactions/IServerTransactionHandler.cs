using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Custom transaction handler implementation
    /// </summary>
    public interface IServerTransactionHandler
    {
        /// <summary>
        /// Called when a transaction container created and being loaded
        /// </summary>
        Task Load(ServerTransactionContainer container);

        /// <summary>
        /// Called when the container is destroyed
        /// </summary>
        Task Destroy(ServerTransactionContainer container);

        /// <summary>
        /// Authentication implementation for transactions.
        /// Before a transaction has started, that method is called
        /// </summary>
        Task<bool> CanCreate(ServerTransaction transaction);

        /// <summary>
        /// Tansaction commit implementation
        /// </summary>
        Task Commit(ServerTransaction transaction);

        /// <summary>
        /// Transaction rollback implementation
        /// </summary>
        Task Rollback(ServerTransaction transaction);

        /// <summary>
        /// Executed when a transaction is timed out
        /// </summary>
        Task Timeout(ServerTransaction transaction);

        /// <summary>
        /// Executed when a transaction is timed out, but sending the message to timeout endpoint is failed
        /// </summary>
        Task TimeoutSendFailed(ServerTransaction transaction, IServerTransactionEndpoint timeoutEndpoint);
    }
}