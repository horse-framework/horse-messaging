using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Custom transaction handler implementation.
/// Except CanCreate, all methods are notifications.
/// Exceptions thrown by notifications are logged and do not change the result of the operation.
/// </summary>
public interface IServerTransactionHandler
{
    /// <summary>
    /// Called before a transaction begins.
    /// If it returns false, the transaction is not created and the client receives Unauthorized.
    /// </summary>
    Task<bool> CanCreate(ServerTransaction transaction);

    /// <summary>
    /// Called after the transaction is delivered to the commit endpoint
    /// </summary>
    Task Commit(ServerTransaction transaction);

    /// <summary>
    /// Called after the transaction is delivered to the rollback endpoint.
    /// The rollback may be requested by the client or done by the server
    /// (client disconnect, server shutdown, container delete or failed commit/timeout delivery).
    /// </summary>
    Task Rollback(ServerTransaction transaction);

    /// <summary>
    /// Called after a timed out transaction is delivered to the timeout endpoint
    /// (or to the rollback endpoint if the container has no timeout endpoint)
    /// </summary>
    Task Timeout(ServerTransaction transaction);

    /// <summary>
    /// Called when sending a finished transaction to an endpoint fails.
    /// If commit or timeout delivery fails, the transaction is rolled back after this call.
    /// </summary>
    /// <param name="transaction">The transaction</param>
    /// <param name="operation">The operation that could not be delivered</param>
    /// <param name="endpoint">The endpoint that failed</param>
    Task SendFailed(ServerTransaction transaction, ServerTransactionStatus operation, IServerTransactionEndpoint endpoint);

    /// <summary>
    /// Called after the container is deleted and its open transactions are rolled back
    /// </summary>
    Task Destroy(ServerTransactionContainer container);
}
