using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Endpoint implementation for transactions.
    /// The endpoint is used for Commit, Rollback, Timeout operations
    /// </summary>
    public interface IServerTransactionEndpoint
    {
        Task<bool> Send(ServerTransaction transaction);
    }
}