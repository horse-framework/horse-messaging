using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client;

/// <summary>
/// Transaction status
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Transaction is not started
    /// </summary>
    None,

    /// <summary>
    /// Transaction began and is open
    /// </summary>
    Begin,

    /// <summary>
    /// Transaction committed
    /// </summary>
    Commit,

    /// <summary>
    /// Transaction rolled back
    /// </summary>
    Rollback,

    /// <summary>
    /// Transaction is not open on the server anymore, but this client does not know how it ended.
    /// It may be finished by an earlier request whose response was lost, timed out,
    /// or rolled back by the server because the client disconnected.
    /// </summary>
    Ended
}

/// <summary>
/// Result of a commit or rollback operation
/// </summary>
public enum TransactionResult
{
    /// <summary>
    /// Transaction is committed
    /// </summary>
    Committed,

    /// <summary>
    /// Transaction is rolled back.
    /// A commit returns this result when the server could not deliver the commit and rolled the transaction back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Transaction is not open on the server.
    /// It is already finished (committed, rolled back, timed out or rolled back because of a disconnect).
    /// </summary>
    NotFound,

    /// <summary>
    /// The request is not completed (no response, send error, server is shutting down).
    /// The transaction may still be open, status stays Begin. The operation can be retried.
    /// </summary>
    Unknown
}

/// <summary>
/// Thrown when a transaction cannot begin
/// </summary>
public class TransactionException : Exception
{
    /// <summary>
    /// Result code of the server, or the client side code if the request is not completed
    /// </summary>
    public HorseResultCode Code { get; }

    /// <summary>
    /// Creates new transaction exception
    /// </summary>
    public TransactionException(HorseResultCode code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Horse transaction.
/// A transaction lives only as long as the connection that began it:
/// if the client disconnects, the server rolls back its open transactions.
/// </summary>
public class HorseTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Transaction name (transaction container name on the server)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Unique transaction Id
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Transaction client
    /// </summary>
    public HorseClient Client { get; }

    /// <summary>
    /// Transaction Status
    /// </summary>
    public TransactionStatus Status => (TransactionStatus) Volatile.Read(ref _status);

    private int _status = (int) TransactionStatus.None;
    private int _beginStarted;
    private byte[] _content;

    /// <summary>
    /// Creates new transaction
    /// </summary>
    /// <param name="client">Transaction client</param>
    /// <param name="name">Transaction name</param>
    public HorseTransaction(HorseClient client, string name)
        : this(client, name, client?.UniqueIdGenerator.Create())
    {
    }

    /// <summary>
    /// Creates new transaction
    /// </summary>
    /// <param name="client">Transaction client</param>
    /// <param name="name">Transaction name</param>
    /// <param name="transactionId">User defined transaction Id. Must be unique in the transaction container.</param>
    public HorseTransaction(HorseClient client, string name, string transactionId)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Transaction name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("Transaction id is required", nameof(transactionId));

        Name = name;
        Client = client;
        Id = transactionId;
    }

    /// <summary>
    /// Creates and begins new transaction.
    /// </summary>
    /// <param name="client">Transaction client.</param>
    /// <param name="name">Transaction name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TransactionException">Thrown when the transaction cannot begin</exception>
    public static async Task<HorseTransaction> Begin(HorseClient client, string name, CancellationToken cancellationToken)
    {
        HorseTransaction transaction = new HorseTransaction(client, name);
        await transaction.Begin(cancellationToken).ConfigureAwait(false);
        return transaction;
    }

    /// <summary>
    /// Disposes the transaction.
    /// If the transaction is still open, a rollback request is sent in background without waiting for it.
    /// Use DisposeAsync (await using) to wait for the rollback.
    /// </summary>
    public void Dispose()
    {
        if (Status != TransactionStatus.Begin)
            return;

        _ = Task.Run(RollbackQuietly);
    }

    /// <summary>
    /// Disposes the transaction.
    /// If the transaction is still open, it is rolled back.
    /// Errors are ignored, the server rolls the transaction back on timeout or disconnect anyway.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Status != TransactionStatus.Begin)
            return;

        await RollbackQuietly().ConfigureAwait(false);
    }

    private async Task RollbackQuietly()
    {
        try
        {
            if (Status == TransactionStatus.Begin && Client.IsConnected)
                await Rollback(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Sets payload content
    /// </summary>
    public void SetMessageContent(byte[] data)
    {
        _content = data;
    }

    /// <summary>
    /// Begins new transaction
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if begin is already called for the transaction</exception>
    /// <exception cref="TransactionException">Thrown when the transaction cannot begin. Code is NotFound if the transaction name is not defined on server.</exception>
    public Task Begin(CancellationToken cancellationToken)
    {
        return Begin<object>(null, cancellationToken);
    }

    /// <summary>
    /// Begins new transaction with a payload model.
    /// If model is null, the content set by SetMessageContent is used.
    /// </summary>
    /// <param name="model">Transaction payload model, if exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="TModel">Transaction payload model type.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if begin is already called for the transaction</exception>
    /// <exception cref="TransactionException">Thrown when the transaction cannot begin. Code is NotFound if the transaction name is not defined on server.</exception>
    public async Task Begin<TModel>(TModel model, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _beginStarted, 1) == 1)
            throw new InvalidOperationException($"Begin is already called for {Name} transaction ({Id}). Create a new transaction to begin again.");

        HorseMessage message = CreateMessage(KnownContentTypes.TransactionBegin);

        if (model != null)
            Client.MessageSerializer.Serialize(message, model);
        else if (_content != null)
            message.Content = new MemoryStream(_content);

        HorseResultCode code;
        try
        {
            HorseMessage response = await Client.Request(message, cancellationToken).ConfigureAwait(false);
            code = (HorseResultCode) response.ContentType;
        }
        catch (OperationCanceledException)
        {
            //the server may have created the transaction
            _ = Task.Run(() => SendRollback(CancellationToken.None));
            throw;
        }

        if (code == HorseResultCode.Ok)
        {
            Volatile.Write(ref _status, (int) TransactionStatus.Begin);
            return;
        }

        //response is not received, the server may have created the transaction
        if (code == HorseResultCode.RequestTimeout)
            _ = Task.Run(() => SendRollback(CancellationToken.None));

        string reason = code switch
        {
            HorseResultCode.NotFound => "transaction name is not defined in server",
            HorseResultCode.Conflict => "there is already an open transaction with same id",
            HorseResultCode.Unauthorized => "transaction is not allowed by server",
            HorseResultCode.Busy => "server is shutting down",
            HorseResultCode.RequestTimeout => "no response from server",
            _ => $"begin operation failed with {code}"
        };

        throw new TransactionException(code, $"{Name} transaction could not begin, {reason} ({Id})");
    }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not open</exception>
    public Task<TransactionResult> Commit(CancellationToken cancellationToken)
    {
        return Finish(true, cancellationToken);
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not open</exception>
    public Task<TransactionResult> Rollback(CancellationToken cancellationToken)
    {
        return Finish(false, cancellationToken);
    }

    private async Task<TransactionResult> Finish(bool commit, CancellationToken cancellationToken)
    {
        TransactionStatus status = Status;
        if (status != TransactionStatus.Begin)
            throw new InvalidOperationException($"{Name} Transaction in {status} status cannot be {(commit ? "committed" : "rolled back")} ({Id})");

        HorseMessage message = CreateMessage(commit ? KnownContentTypes.TransactionCommit : KnownContentTypes.TransactionRollback);
        HorseMessage response = await Client.Request(message, cancellationToken).ConfigureAwait(false);
        HorseResultCode code = (HorseResultCode) response.ContentType;

        switch (code)
        {
            case HorseResultCode.Ok:
                SetFinalStatus(commit ? TransactionStatus.Commit : TransactionStatus.Rollback);
                return commit ? TransactionResult.Committed : TransactionResult.RolledBack;

            //the server finished the transaction but could not deliver the operation, a failed commit is rolled back
            case HorseResultCode.Failed:
                SetFinalStatus(TransactionStatus.Rollback);
                return TransactionResult.RolledBack;

            case HorseResultCode.NotFound:
                SetFinalStatus(TransactionStatus.Ended);
                return TransactionResult.NotFound;

            default:
                return TransactionResult.Unknown;
        }
    }

    private async Task SendRollback(CancellationToken cancellationToken)
    {
        try
        {
            if (Client.IsConnected)
                await Client.Request(CreateMessage(KnownContentTypes.TransactionRollback), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void SetFinalStatus(TransactionStatus status)
    {
        Interlocked.CompareExchange(ref _status, (int) status, (int) TransactionStatus.Begin);
    }

    private HorseMessage CreateMessage(ushort contentType)
    {
        HorseMessage message = new HorseMessage(MessageType.Transaction, Name, contentType);
        message.AddHeader(HorseHeaders.TRANSACTION_ID, Id);
        return message;
    }
}
