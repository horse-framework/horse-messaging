using System.Threading.Tasks;

namespace Horse.Messaging.Server.Transactions
{
    public interface IServerTransactionEndpoint
    {
        Task<bool> Send(ServerTransaction transaction);
    }
}