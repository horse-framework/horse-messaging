using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Container for transactions with same name
    /// </summary>
    public class ServerTransactionContainer
    {
        /// <summary>
        /// Container name.
        /// It can be used like transaction type name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Timeout duration for transactions for the container
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Endpoint for committed transactions
        /// </summary>
        public IServerTransactionEndpoint CommitEndpoint { get; set; }

        /// <summary>
        /// Endpoint for rolled back transactions
        /// </summary>
        public IServerTransactionEndpoint RollbackEndpoint { get; set; }

        /// <summary>
        /// Endpoint for timed out transactions
        /// </summary>
        public IServerTransactionEndpoint TimeoutEndpoint { get; set; }

        /// <summary>
        /// Custom transaction handler implementation for the container
        /// </summary>
        public IServerTransactionHandler Handler { get; set; }

        private readonly Dictionary<string, ServerTransaction> _transactions = new Dictionary<string, ServerTransaction>();

        /// <summary>
        /// Creates new server transaction handler
        /// </summary>
        /// <param name="name">Name for transactions</param>
        /// <param name="timeout">Transaction timeout</param>
        public ServerTransactionContainer(string name, TimeSpan timeout)
        {
            Name = name;
            Timeout = timeout;
        }

        /// <summary>
        /// Loads all transactions
        /// </summary>
        public Task Load()
        {
            if (Handler != null)
                return Handler.Load(this);

            lock (_transactions)
                foreach (ServerTransaction transaction in _transactions.Values)
                {
                    transaction.TimeoutHandler.Task.ContinueWith(HandleTransaction);
                    transaction.Track();
                }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds a transaction to the container.
        /// This method SHOULD be used to load previous transactions after application restart. 
        /// </summary>
        /// <param name="id">Transaction Id</param>
        /// <param name="deadline">Transaction deadline</param>
        /// <param name="messageContent">Message content for transaction</param>
        public async Task<bool> Add(string id, DateTime deadline, MemoryStream messageContent)
        {
            HorseMessage message = new HorseMessage();
            message.MessageId = id;
            message.Content = messageContent;

            ServerTransaction transaction = new ServerTransaction(null, message, deadline);

            if (Handler != null)
            {
                bool canCreate = await Handler.CanCreate(transaction);
                if (!canCreate)
                    return false;
            }

            lock (_transactions)
                _transactions.Add(message.MessageId, transaction);

            _ = transaction.TimeoutHandler.Task.ContinueWith(HandleTransaction);
            transaction.Track();
            
            return true;
        }

        /// <summary>
        /// Creates new transaction from a request
        /// </summary>
        public async Task<bool> Create(MessagingClient client, HorseMessage message)
        {
            ServerTransaction transaction = new ServerTransaction(client, message, DateTime.UtcNow.Add(Timeout));
                
            if (Handler != null)
            {
                bool canCreate = await Handler.CanCreate(transaction);
                if (!canCreate)
                    return false;
            }

            lock (_transactions)
                _transactions.Add(message.MessageId, transaction);

            _ = transaction.TimeoutHandler.Task.ContinueWith(HandleTransaction);
            transaction.Track();
            
            return true;
        }

        /// <summary>
        /// Called when a client commits a transaction belong this container
        /// </summary>
        public async Task Commit(MessagingClient client, HorseMessage message)
        {
            HorseMessage response;
            ServerTransaction transaction;

            lock (_transactions)
                _transactions.TryGetValue(message.MessageId, out transaction);

            if (transaction == null)
            {
                response = message.CreateResponse(HorseResultCode.NotFound);
                await client.SendAsync(response);
                return;
            }

            bool commited = await CommitEndpoint.Send(transaction);
            if (!commited)
            {
                response = message.CreateResponse(HorseResultCode.Failed);
                await client.SendAsync(response);
                return;
            }

            if (Handler != null)
                await Handler.Commit(transaction);

            transaction.Status = ServerTransactionStatus.Commit;

            response = message.CreateResponse(HorseResultCode.Ok);
            await client.SendAsync(response);
        }


        /// <summary>
        /// Called when a client rolls back transaction belong this container
        /// </summary>
        public async Task Rollback(MessagingClient client, HorseMessage message)
        {
            HorseMessage response;
            ServerTransaction transaction;

            lock (_transactions)
                _transactions.TryGetValue(message.MessageId, out transaction);

            if (transaction == null)
            {
                response = message.CreateResponse(HorseResultCode.NotFound);
                await client.SendAsync(response);
                return;
            }

            bool rolledback = await RollbackEndpoint.Send(transaction);
            if (!rolledback)
            {
                response = message.CreateResponse(HorseResultCode.Failed);
                await client.SendAsync(response);
                return;
            }

            if (Handler != null)
                await Handler.Rollback(transaction);

            transaction.Status = ServerTransactionStatus.Rollback;

            response = message.CreateResponse(HorseResultCode.Ok);
            await client.SendAsync(response);
        }

        private async Task HandleTransaction(Task<ServerTransaction> task)
        {
            try
            {
                if (task.Result.Status == ServerTransactionStatus.Timeout)
                {
                    bool sent = await TimeoutEndpoint.Send(task.Result);
                    if (sent)
                        Handler?.Timeout(task.Result);
                    else
                        Handler?.TimeoutSendFailed(task.Result, TimeoutEndpoint);
                }
            }
            catch
            {
            }
        }
    }
}