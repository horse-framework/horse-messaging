using System;
using System.Threading;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Represents a single transaction in server.
/// A transaction is open until it is committed, rolled back or timed out,
/// or until its client disconnects (then it is rolled back).
/// </summary>
public class ServerTransaction
{
    /// <summary>
    /// Unique transaction id. It is the value of the Transaction-Id header.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Container of the transaction
    /// </summary>
    public ServerTransactionContainer Container { get; }

    /// <summary>
    /// Transaction deadline in UTC
    /// </summary>
    public DateTime Deadline { get; }

    /// <summary>
    /// The message that begins the transaction.
    /// Its content and headers are the payload of the transaction.
    /// </summary>
    public HorseMessage Message { get; }

    /// <summary>
    /// Owner client of the transaction.
    /// Only the owner client can commit or roll back the transaction.
    /// </summary>
    public MessagingClient Client { get; }

    private int _status = (int) ServerTransactionStatus.Begin;

    /// <summary>
    /// Transaction status.
    /// It is Begin while the transaction is open.
    /// When the transaction is finished, it is the operation that finished it.
    /// </summary>
    public ServerTransactionStatus Status
    {
        get => (ServerTransactionStatus) Volatile.Read(ref _status);
        internal set => Volatile.Write(ref _status, (int) value);
    }

    internal ServerTransaction(ServerTransactionContainer container, MessagingClient client, HorseMessage message, string id, DateTime deadline)
    {
        Container = container;
        Client = client;
        Message = message;
        Id = id;
        Deadline = deadline;
    }
}
