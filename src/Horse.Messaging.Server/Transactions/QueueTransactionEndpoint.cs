using System.Threading.Tasks;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Queue transaction endpoint pushes the message to a queue
    /// </summary>
    public class QueueTransactionEndpoint : IServerTransactionEndpoint
    {
        private HorseQueue _queue;
        private readonly QueueRider _rider;
        private readonly string _queueName;

        /// <summary>
        /// Queue name
        /// </summary>
        public string InitParameter => _queue?.Name;

        /// <summary>
        /// Creates new queue transaction endpoint
        /// </summary>
        public QueueTransactionEndpoint(HorseQueue queue)
        {
            _queue = queue;
        }

        /// <summary>
        /// Creates new queue transaction endpoint
        /// </summary>
        public QueueTransactionEndpoint(QueueRider rider, string queueName)
        {
            _rider = rider;
            _queueName = queueName;
            _queue = rider.Find(queueName);
        }

        /// <summary>
        /// Pushes a message into the target queue
        /// </summary>
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