using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Represents a single transaction in server
    /// </summary>
    public class ServerTransaction
    {
        /// <summary>
        /// Unique Transaction Id
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Transaction deadline
        /// </summary>
        public DateTime Deadline { get; }

        /// <summary>
        /// The message that starts the transaction
        /// </summary>
        public HorseMessage Message { get; }
        
        /// <summary>
        /// Transaction client
        /// </summary>
        public MessagingClient Client { get; }

        internal TaskCompletionSource<ServerTransaction> TimeoutHandler { get; } = new TaskCompletionSource<ServerTransaction>();

        private ServerTransactionStatus _status;
        private Task _waiterTask;
        private CancellationTokenSource _waiterCancellation;

        /// <summary>
        /// Transaction status
        /// </summary>
        public ServerTransactionStatus Status
        {
            get => _status;
            internal set
            {
                _status = value;

                if (Status == ServerTransactionStatus.None || Status == ServerTransactionStatus.Begin)
                    return;

                if (_waiterTask == null || _waiterCancellation == null || _waiterTask.IsCompleted)
                    return;

                try
                {
                    TimeoutHandler.SetResult(this);
                    _waiterCancellation.Cancel();
                    _waiterCancellation.Dispose();
                    _waiterTask.Dispose();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Creates new transaction
        /// </summary>
        public ServerTransaction(MessagingClient client, HorseMessage messsage, DateTime deadline)
        {
            Client = client;
            Message = messsage;
            Id = messsage.MessageId;
            Deadline = deadline;
            Status = ServerTransactionStatus.Begin;
        }

        internal void Track()
        {
            _waiterCancellation = new CancellationTokenSource();
            _waiterTask = Task.Run(async () =>
            {
                try
                {
                    int milliseconds = Convert.ToInt32((Deadline - DateTime.UtcNow).TotalMilliseconds);
                    
                    if (milliseconds > 0)
                        await Task.Delay(milliseconds, _waiterCancellation.Token);

                    if (Status == ServerTransactionStatus.Begin)
                    {
                        _status = ServerTransactionStatus.Rollback;
                        TimeoutHandler.SetResult(this);
                    }

                    _waiterCancellation.Dispose();
                    _waiterTask.Dispose();
                }
                catch
                {
                }
            }, _waiterCancellation.Token);
        }
    }
}