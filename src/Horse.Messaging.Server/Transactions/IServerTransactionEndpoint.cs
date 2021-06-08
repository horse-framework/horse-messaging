using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions
{
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
        /// Sends message for transaction to the endpoint 
        /// </summary>
        Task<bool> Send(ServerTransaction transaction);
    }
}