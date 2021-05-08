using System.Threading.Tasks;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Transactions
{
    public class QueueTransactionEndpoint : IServerTransactionEndpoint
    {
        private QueueRider _rider;
        private HorseQueue _queue;
        private string _queueName;
        
        public QueueTransactionEndpoint(HorseQueue queue)
        {
            _queue = queue;
        }

        public QueueTransactionEndpoint(QueueRider rider, string queueName)
        {
            _rider = rider;
            _queueName = queueName;
            _queue = rider.Find(queueName);
        }
        
        public async Task<bool> Send(ServerTransaction transaction)
        {
            try
            {
                if (_queue == null)
                {
                    if (_rider != null && !string.IsNullOrEmpty(_queueName))
                        _queue = await _rider.Create(_queueName);

                    if (_queue == null)
                        return false;
                }

                PushResult result = await _queue.Push(transaction.Message);
                return result == PushResult.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}