using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Transactions;
using Test.Transactions.Core;
using Xunit;

namespace Test.Transactions;

public class TransactionShutdownTest
{
    /// <summary>
    /// Endpoint that runs a callback for each transaction
    /// </summary>
    private class CallbackEndpoint : IServerTransactionEndpoint
    {
        private readonly Func<ServerTransaction, Task<bool>> _send;

        public string InitParameter => null;

        public CallbackEndpoint(Func<ServerTransaction, Task<bool>> send)
        {
            _send = send;
        }

        public Task<bool> Send(ServerTransaction transaction) => _send(transaction);
    }

    private static async Task<int> LoadMessageCount(string filename)
    {
        if (!File.Exists(filename))
            return 0;

        Database database = new Database(new DatabaseOptions
        {
            Filename = filename,
            AutoFlush = false,
            AutoShrink = false,
            InstantFlush = true
        });

        try
        {
            return (await database.Open()).Count;
        }
        finally
        {
            await database.Close();
        }
    }

    private static async Task PushAndWait(HorseClient client, string queue, string content)
    {
        HorseResult result = await client.Queue.Push(queue, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    [Fact]
    public async Task TransactionShutdown_DoesNotWaitLongerThanTimeout()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        TaskCompletionSource<bool> never = new TaskCompletionSource<bool>();
        RecordingEndpoint commit = new RecordingEndpoint($"commit-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), commit, new CallbackEndpoint(_ => never.Task));
        HorseClient client = await server.Connect();

        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool finished = await server.Rider.Transaction.ShutdownAsync(TimeSpan.FromMilliseconds(500));
        stopwatch.Stop();

        Assert.False(finished);
        Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"shutdown took {stopwatch.ElapsedMilliseconds} ms");
        Assert.Contains(server.Errors.Messages, x => x.Contains("did not finish"));

        never.SetResult(true);
    }

    [Fact]
    public async Task QueueShutdown_FlushesQueues_RollsBackTransactions_AndFlushesRollbackMessages()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start(persistentQueues: true);
        await server.Rider.Queue.Create("sd-data");
        await server.Rider.Queue.Create("sd-rollback");

        string dataFile = Path.Combine(server.DataPath, "sd-data.hdb");
        string rollbackFile = Path.Combine(server.DataPath, "sd-rollback.hdb");
        long dataLengthWhenRollbackStarted = -1;

        QueueTransactionEndpoint rollbackQueue = new QueueTransactionEndpoint(server.Rider.Queue, "sd-rollback");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
            new QueueTransactionEndpoint(server.Rider.Queue, "sd-commit"),
            new CallbackEndpoint(async transaction =>
            {
                //queue data must be written to disk before transactions are rolled back.
                //the store is still open here, so only the file length is checked.
                dataLengthWhenRollbackStarted = new FileInfo(dataFile).Length;
                return await rollbackQueue.Send(transaction);
            }));

        HorseClient client = await server.Connect();
        await PushAndWait(client, "sd-data", "before-shutdown");
        Assert.Equal(0, new FileInfo(dataFile).Length);

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        transaction.SetMessageContent("rollback-payload"u8.ToArray());
        await transaction.Begin(CancellationToken.None);

        await server.Rider.Queue.ShutdownAsync(TimeSpan.Zero);

        Assert.True(dataLengthWhenRollbackStarted > 0, $"queue data was not on disk when rollback started (length {dataLengthWhenRollbackStarted})");
        Assert.Equal(1, await LoadMessageCount(dataFile));
        Assert.Equal(1, await LoadMessageCount(rollbackFile));
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);

        //client pushes and transaction requests are rejected during shutdown
        HorseResult push = await client.Queue.Push("sd-data", new MemoryStream("after-shutdown"u8.ToArray()), true, CancellationToken.None);
        Assert.NotEqual(HorseResultCode.Ok, push.Code);

        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => new HorseTransaction(client, "tx").Begin(CancellationToken.None));
        Assert.Equal(HorseResultCode.Busy, exception.Code);
    }

    [Fact]
    public async Task QueueShutdown_WaitsForRollbackStartedByDisconnect()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start(persistentQueues: true);
        await server.Rider.Queue.Create("sd-rollback");
        string rollbackFile = Path.Combine(server.DataPath, "sd-rollback.hdb");

        QueueTransactionEndpoint rollbackQueue = new QueueTransactionEndpoint(server.Rider.Queue, "sd-rollback");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
            new RecordingEndpoint($"commit-{Guid.NewGuid():N}"),
            new CallbackEndpoint(async transaction =>
            {
                await Task.Delay(1000);
                return await rollbackQueue.Send(transaction);
            }));

        HorseClient client = await server.Connect();
        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);

        //the disconnect claims the transaction and its slow rollback is in progress when shutdown starts
        client.Disconnect();
        Assert.True(await Wait.Until(() => server.Rider.Transaction.Find("tx").Count == 0, 3000));

        await server.Rider.Queue.ShutdownAsync(TimeSpan.Zero);

        Assert.Equal(1, await LoadMessageCount(rollbackFile));
    }

    [Fact]
    public async Task QueueShutdown_WaitsForCommitInProgress()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start(persistentQueues: true);
        await server.Rider.Queue.Create("sd-commit");
        string commitFile = Path.Combine(server.DataPath, "sd-commit.hdb");

        QueueTransactionEndpoint commitQueue = new QueueTransactionEndpoint(server.Rider.Queue, "sd-commit");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
            new CallbackEndpoint(async transaction =>
            {
                await Task.Delay(1000);
                return await commitQueue.Send(transaction);
            }),
            new RecordingEndpoint($"rollback-{Guid.NewGuid():N}"));

        HorseClient client = await server.Connect();
        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Task<TransactionResult> commit = transaction.Commit(CancellationToken.None);
        Assert.True(await Wait.Until(() => server.Rider.Transaction.Find("tx").Count == 0, 3000));

        await server.Rider.Queue.ShutdownAsync(TimeSpan.Zero);

        Assert.Equal(TransactionResult.Committed, await commit);
        Assert.Equal(1, await LoadMessageCount(commitFile));
    }

    [Fact]
    public async Task QueueShutdown_IsNotBlocked_ByHangingRollback()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start(persistentQueues: true);
        await server.Rider.Queue.Create("sd-data");
        string dataFile = Path.Combine(server.DataPath, "sd-data.hdb");

        TaskCompletionSource<bool> never = new TaskCompletionSource<bool>();
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
            new RecordingEndpoint($"commit-{Guid.NewGuid():N}"),
            new CallbackEndpoint(_ => never.Task));

        HorseClient client = await server.Connect();
        await PushAndWait(client, "sd-data", "before-shutdown");
        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await server.Rider.Queue.ShutdownAsync(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"shutdown took {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(1, await LoadMessageCount(dataFile));

        never.SetResult(true);
    }

    [Fact]
    public async Task QueueShutdown_IsCompletedOnce()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint rollback = new RecordingEndpoint($"rollback-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), new RecordingEndpoint($"commit-{Guid.NewGuid():N}"), rollback);
        HorseClient client = await server.Connect();
        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);

        Task first = server.Rider.Queue.ShutdownAsync(TimeSpan.Zero);
        Task second = server.Rider.Queue.ShutdownAsync(TimeSpan.Zero);

        Assert.Same(first, second);
        await first;
        Assert.Equal(1, rollback.Count);
    }
}
