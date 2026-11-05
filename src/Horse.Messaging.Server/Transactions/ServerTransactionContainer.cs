using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Container for transactions with same name.
/// </summary>
/// <remarks>
/// A transaction is finished exactly once. Commit, rollback, timeout, client disconnect,
/// server shutdown and container delete all try to remove the transaction from the container
/// and only the one that removes it finishes the transaction. Finished transactions are not kept,
/// so later requests for the same transaction receive NotFound.
/// </remarks>
public class ServerTransactionContainer
{
    private readonly record struct DeadlineEntry(string Id, DateTime Deadline);

    /// <summary>
    /// Container name.
    /// It can be used like transaction type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Timeout duration for transactions of the container
    /// </summary>
    public TimeSpan Timeout { get; internal set; }

    /// <summary>
    /// Endpoint for committed transactions
    /// </summary>
    public IServerTransactionEndpoint CommitEndpoint { get; internal set; }

    /// <summary>
    /// Endpoint for rolled back transactions.
    /// Transactions whose commit or timeout delivery fails are sent to this endpoint too.
    /// </summary>
    public IServerTransactionEndpoint RollbackEndpoint { get; internal set; }

    /// <summary>
    /// Endpoint for timed out transactions.
    /// If null, timed out transactions are sent to the rollback endpoint.
    /// </summary>
    public IServerTransactionEndpoint TimeoutEndpoint { get; internal set; }

    /// <summary>
    /// Custom transaction handler for the container.
    /// If null, TransactionRider.DefaultHandler is used.
    /// </summary>
    public IServerTransactionHandler Handler { get; internal set; }

    /// <summary>
    /// Number of open transactions in the container
    /// </summary>
    public int Count => _transactions.Count;

    private readonly TransactionRider _rider;
    private readonly ConcurrentDictionary<string, ServerTransaction> _transactions = new();

    // Transactions are added with a constant timeout, so deadlines are queued in (almost) ascending order.
    // The rider's sweeper only reads the head of the queue, so a timeout check costs nothing per open transaction.
    private readonly ConcurrentQueue<DeadlineEntry> _deadlines = new();

    private volatile bool _destroyed;

    internal ServerTransactionContainer(TransactionRider rider, string name, TimeSpan timeout)
    {
        _rider = rider;
        Name = name;
        Timeout = timeout;
    }

    /// <summary>
    /// Finds an open transaction
    /// </summary>
    public ServerTransaction Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        _transactions.TryGetValue(id, out ServerTransaction transaction);
        return transaction;
    }

    /// <summary>
    /// Returns all open transactions
    /// </summary>
    public ServerTransaction[] GetTransactions()
    {
        return _transactions.Values.ToArray();
    }

    private IServerTransactionHandler GetHandler() => Handler ?? _rider.DefaultHandler;

    #region Operations

    /// <summary>
    /// Begins new transaction
    /// </summary>
    internal async Task<HorseResultCode> Begin(MessagingClient client, HorseMessage message, string id)
    {
        if (_destroyed)
            return HorseResultCode.NotFound;

        ServerTransaction transaction = new ServerTransaction(this, client, message, id, DateTime.UtcNow.Add(Timeout));

        IServerTransactionHandler handler = GetHandler();
        if (handler != null && !await handler.CanCreate(transaction))
            return HorseResultCode.Unauthorized;

        if (!_transactions.TryAdd(id, transaction))
            return HorseResultCode.Conflict;

        _deadlines.Enqueue(new DeadlineEntry(id, transaction.Deadline));
        _rider.TrackClientTransaction(transaction);

        // the container is deleted or the server started shutting down while the transaction was being created.
        // the client is told that the transaction is not created, so it is removed without any delivery.
        if (_destroyed || _rider.IsShuttingDown)
        {
            if (TryClaim(transaction))
                _rider.EndCompletion();

            return _destroyed ? HorseResultCode.NotFound : HorseResultCode.Busy;
        }

        return HorseResultCode.Ok;
    }

    /// <summary>
    /// Finishes a transaction by its owner client.
    /// Returns Ok if the operation is delivered,
    /// Failed if it is not delivered (a failed commit is rolled back),
    /// NotFound if the transaction is not open or the client is not its owner.
    /// </summary>
    internal async Task<HorseResultCode> Finish(MessagingClient client, string id, ServerTransactionStatus operation)
    {
        if (!_transactions.TryGetValue(id, out ServerTransaction transaction) || !ReferenceEquals(transaction.Client, client))
            return HorseResultCode.NotFound;

        if (!TryClaim(transaction))
            return HorseResultCode.NotFound;

        bool delivered = await TrackedComplete(transaction, operation);
        return delivered ? HorseResultCode.Ok : HorseResultCode.Failed;
    }

    /// <summary>
    /// Finishes a transaction by server (timeout, client disconnect, shutdown, container delete).
    /// Does nothing if the transaction is already finished.
    /// </summary>
    internal async Task FinishByServer(ServerTransaction transaction, ServerTransactionStatus operation)
    {
        if (!TryClaim(transaction))
            return;

        try
        {
            await TrackedComplete(transaction, operation);
        }
        catch (Exception e)
        {
            _rider.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionFinish,
                $"Transaction {Name}/{transaction.Id} could not be finished with {operation}", e);
        }
    }

    /// <summary>
    /// Rolls back all open transactions
    /// </summary>
    internal Task RollbackAll()
    {
        ServerTransaction[] transactions = _transactions.Values.ToArray();
        if (transactions.Length == 0)
            return Task.CompletedTask;

        return Task.WhenAll(transactions.Select(x => FinishByServer(x, ServerTransactionStatus.Rollback)));
    }

    /// <summary>
    /// Marks the container as deleted. New transactions are not accepted after this call.
    /// </summary>
    internal void MarkDestroyed()
    {
        _destroyed = true;
    }

    /// <summary>
    /// Finishes the transactions whose deadline has passed.
    /// Called by the rider's sweeper, only from a single thread at a time.
    /// </summary>
    internal void CheckTimeouts(DateTime now)
    {
        while (_deadlines.TryPeek(out DeadlineEntry entry) && entry.Deadline <= now)
        {
            if (!_deadlines.TryDequeue(out entry))
                break;

            // the transaction may be finished already or the id may belong to a newer transaction with a later deadline
            if (!_transactions.TryGetValue(entry.Id, out ServerTransaction transaction) || transaction.Deadline > now)
                continue;

            _ = Task.Run(() => FinishByServer(transaction, ServerTransactionStatus.Timeout));
        }
    }

    #endregion

    #region Completion

    /// <summary>
    /// Removes the transaction from the container.
    /// Only one caller can remove a transaction, so only one operation can finish it.
    /// If it returns true, the caller must complete the transaction with TrackedComplete
    /// or call the rider's EndCompletion.
    /// </summary>
    private bool TryClaim(ServerTransaction transaction)
    {
        _rider.BeginCompletion();

        if (!_transactions.TryRemove(new KeyValuePair<string, ServerTransaction>(transaction.Id, transaction)))
        {
            _rider.EndCompletion();
            return false;
        }

        _rider.UntrackClientTransaction(transaction);
        return true;
    }

    /// <summary>
    /// Completes a claimed transaction and ends the completion started by TryClaim,
    /// so shutdown waits for it whoever started it (client request, disconnect, timeout).
    /// </summary>
    private async Task<bool> TrackedComplete(ServerTransaction transaction, ServerTransactionStatus operation)
    {
        try
        {
            return await Complete(transaction, operation);
        }
        finally
        {
            _rider.EndCompletion();
        }
    }

    /// <summary>
    /// Delivers a claimed transaction to the endpoint of the operation.
    /// If commit or timeout delivery fails, the transaction is rolled back.
    /// Returns true if the requested operation is delivered.
    /// </summary>
    private async Task<bool> Complete(ServerTransaction transaction, ServerTransactionStatus operation)
    {
        IServerTransactionEndpoint endpoint = operation switch
        {
            ServerTransactionStatus.Commit => CommitEndpoint,
            ServerTransactionStatus.Timeout => TimeoutEndpoint ?? RollbackEndpoint,
            _ => RollbackEndpoint
        };

        transaction.Status = operation;

        if (await Send(transaction, endpoint))
        {
            await Notify(transaction, operation);
            return true;
        }

        await NotifySendFailed(transaction, operation, endpoint);

        if (operation == ServerTransactionStatus.Rollback || ReferenceEquals(endpoint, RollbackEndpoint))
            return false;

        transaction.Status = ServerTransactionStatus.Rollback;

        if (await Send(transaction, RollbackEndpoint))
            await Notify(transaction, ServerTransactionStatus.Rollback);
        else
            await NotifySendFailed(transaction, ServerTransactionStatus.Rollback, RollbackEndpoint);

        return false;
    }

    private async Task<bool> Send(ServerTransaction transaction, IServerTransactionEndpoint endpoint)
    {
        if (endpoint == null)
            return false;

        try
        {
            return await endpoint.Send(transaction);
        }
        catch (Exception e)
        {
            _rider.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionEndpointSend,
                $"Transaction {Name}/{transaction.Id} could not be sent to {endpoint.GetType().Name}", e);
            return false;
        }
    }

    private async Task Notify(ServerTransaction transaction, ServerTransactionStatus operation)
    {
        IServerTransactionHandler handler = GetHandler();
        if (handler == null)
            return;

        try
        {
            switch (operation)
            {
                case ServerTransactionStatus.Commit:
                    await handler.Commit(transaction);
                    break;

                case ServerTransactionStatus.Rollback:
                    await handler.Rollback(transaction);
                    break;

                case ServerTransactionStatus.Timeout:
                    await handler.Timeout(transaction);
                    break;
            }
        }
        catch (Exception e)
        {
            _rider.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionHandler,
                $"Transaction handler {operation} failed for {Name}/{transaction.Id}", e);
        }
    }

    private async Task NotifySendFailed(ServerTransaction transaction, ServerTransactionStatus operation, IServerTransactionEndpoint endpoint)
    {
        _rider.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionEndpointSend,
            $"Transaction {Name}/{transaction.Id} could not be delivered for {operation}", null);

        IServerTransactionHandler handler = GetHandler();
        if (handler == null)
            return;

        try
        {
            await handler.SendFailed(transaction, operation, endpoint);
        }
        catch (Exception e)
        {
            _rider.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionHandler,
                $"Transaction handler SendFailed failed for {Name}/{transaction.Id}", e);
        }
    }

    #endregion
}
