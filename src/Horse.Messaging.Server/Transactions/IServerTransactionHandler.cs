using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions
{
    public interface IServerTransactionHandler
    {
        
        Task Load(ServerTransactionContainer container);
        
        Task<bool> CanCreate(ServerTransaction transaction);

        Task Commit(ServerTransaction transaction);
        
        Task Rollback(ServerTransaction transaction);
        
        Task Timeout(ServerTransaction transaction);
    }
}