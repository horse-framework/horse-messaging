using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Endpoint implementation for transactions.
/// The endpoint is used for Commit, Rollback, Timeout operations
/// </summary>
public interface IServerTransactionEndpoint
{
    /// <summary>
    /// Init parameter is required for loading saved containers.
    /// That parameter will be used as ctor parameter for the type.
    /// Leave null if your type does not require any parameter in ctor.
    /// </summary>
    public string InitParameter { get; }

    /// <summary>
    /// Sends the transaction to the endpoint.
    /// Returns true if the transaction is delivered.
    /// A transaction is sent to the same endpoint at most once.
    /// Exceptions are treated as failed delivery.
    /// </summary>
    Task<bool> Send(ServerTransaction transaction);
}
