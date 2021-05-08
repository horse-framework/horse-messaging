using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Transactions
{
    public enum ServerTransactionStatus
    {
        None,
        Begin,
        Commit,
        Rollback,
        Timeout
    }

    public class ServerTransaction
    {
        public string Id { get; }
        public DateTime Deadline { get; }

        public HorseMessage Message { get; }
        public MessagingClient Client { get; }

        internal TaskCompletionSource<ServerTransaction> TimeoutHandler { get; } = new TaskCompletionSource<ServerTransaction>(TaskCreationOptions.LongRunning);

        private ServerTransactionStatus _status;
        private Task _waiterTask;
        private CancellationTokenSource _waiterCancellation;

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

        public ServerTransaction(MessagingClient client, HorseMessage messsage, DateTime deadline)
        {
            Client = client;
            Message = messsage;
            Id = messsage.MessageId;
            Deadline = deadline;
            Status = ServerTransactionStatus.None;
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