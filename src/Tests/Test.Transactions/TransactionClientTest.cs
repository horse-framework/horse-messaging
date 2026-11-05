using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Transactions;
using Test.Transactions.Core;
using Xunit;

namespace Test.Transactions;

public class TransactionClientTest
{
    [Fact]
    public async Task LateCommitResponse_IsNotMatchedToRollback()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint commit = new RecordingEndpoint($"commit-{Guid.NewGuid():N}") { DelayMs = 1500 };
        RecordingEndpoint rollback = new RecordingEndpoint($"rollback-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), commit, rollback);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        using (CancellationTokenSource cts = new CancellationTokenSource(200))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transaction.Commit(cts.Token));

        Assert.Equal(TransactionStatus.Begin, transaction.Status);

        //the commit is already being processed, so the rollback cannot finish the transaction
        TransactionResult result = await transaction.Rollback(CancellationToken.None);

        Assert.Equal(TransactionResult.NotFound, result);
        Assert.Equal(TransactionStatus.Ended, transaction.Status);
        Assert.True(await Wait.Until(() => commit.Count == 1, 3000));
        Assert.Equal(0, rollback.Count);
    }

    [Fact]
    public async Task Begin_OnNotConnectedClient_Throws()
    {
        HorseTransaction transaction = new HorseTransaction(new HorseClient(), "tx");

        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => transaction.Begin(CancellationToken.None));

        Assert.NotEqual(HorseResultCode.Ok, exception.Code);
        Assert.Equal(TransactionStatus.None, transaction.Status);
    }

    [Fact]
    public async Task Begin_CanBeCalledOnce()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint endpoint = new RecordingEndpoint($"endpoint-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), endpoint, endpoint);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = await HorseTransaction.Begin(client, "tx", CancellationToken.None);

        Assert.Equal(TransactionStatus.Begin, transaction.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.Begin(CancellationToken.None));
    }

    [Fact]
    public async Task Dispose_DoesNotBlockSynchronizationContext_AndRollsBack()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint commit = new RecordingEndpoint($"commit-{Guid.NewGuid():N}");
        RecordingEndpoint rollback = new RecordingEndpoint($"rollback-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), commit, rollback);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Thread thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedSynchronizationContext());
            transaction.Dispose();
        }) { IsBackground = true };

        thread.Start();

        Assert.True(thread.Join(2000));
        Assert.True(await Wait.Until(() => rollback.Count == 1, 3000));
        Assert.Equal(0, commit.Count);
    }

    [Fact]
    public async Task DisposeAsync_RollsBackOpenTransaction()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint commit = new RecordingEndpoint($"commit-{Guid.NewGuid():N}");
        RecordingEndpoint rollback = new RecordingEndpoint($"rollback-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), commit, rollback);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await using (transaction)
            await transaction.Begin(CancellationToken.None);

        Assert.Equal(TransactionStatus.Rollback, transaction.Status);
        Assert.Equal(1, rollback.Count);

        //disposing a committed transaction does nothing
        HorseTransaction committed = new HorseTransaction(client, "tx");
        await using (committed)
        {
            await committed.Begin(CancellationToken.None);
            await committed.Commit(CancellationToken.None);
        }

        Assert.Equal(1, commit.Count);
        Assert.Equal(1, rollback.Count);
    }

    [Fact]
    public async Task Dispose_AfterDisconnect_DoesNotThrow()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint commit = new RecordingEndpoint($"commit-{Guid.NewGuid():N}");
        RecordingEndpoint rollback = new RecordingEndpoint($"rollback-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30), commit, rollback);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        client.Disconnect();

        transaction.Dispose();
        await transaction.DisposeAsync();

        //rolled back by the server because of the disconnect
        Assert.True(await Wait.Until(() => rollback.Count == 1, 3000));
    }

    [Fact]
    public async Task QueueEndpoint_PushesNewMessageWithTransactionId()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
            new QueueTransactionEndpoint(server.Rider.Queue, "tx-commit"),
            new QueueTransactionEndpoint(server.Rider.Queue, "tx-rollback"));

        HorseClient consumer = await server.Connect();
        ConcurrentQueue<HorseMessage> received = new ConcurrentQueue<HorseMessage>();
        consumer.MessageReceived += (_, message) =>
        {
            if (message.Type == MessageType.QueueMessage)
                received.Enqueue(message);
        };

        HorseResult subscribe = await consumer.Queue.Subscribe("tx-commit", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, subscribe.Code);

        HorseClient producer = await server.Connect();
        HorseTransaction transaction = new HorseTransaction(producer, "tx");
        transaction.SetMessageContent("payload"u8.ToArray());

        //custom headers are carried, queue control headers are not
        HorseMessage begin = new HorseMessage(MessageType.Transaction, "tx", KnownContentTypes.TransactionBegin);
        begin.AddHeader(HorseHeaders.TRANSACTION_ID, transaction.Id);
        begin.AddHeader("Custom-Header", "custom-value");
        begin.AddHeader(HorseHeaders.QUEUE_TYPE, "pull");
        begin.SetStringContent("payload");
        HorseMessage response = await producer.Request(begin, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, (HorseResultCode) response.ContentType);

        Assert.Equal(HorseResultCode.Ok, await Raw.Send(producer, "tx", KnownContentTypes.TransactionCommit, transaction.Id));
        Assert.True(await Wait.Until(() => received.Count == 1, 3000));

        Assert.True(received.TryDequeue(out HorseMessage message));
        Assert.Equal(transaction.Id, message.MessageId);
        Assert.Equal("tx-commit", message.Target);
        Assert.Equal(transaction.Id, message.FindHeader(HorseHeaders.TRANSACTION_ID));
        Assert.Equal("custom-value", message.FindHeader("Custom-Header"));
        Assert.Null(message.FindHeader(HorseHeaders.QUEUE_TYPE));
        Assert.Equal("payload", message.GetStringContent());

        Assert.Equal(1, server.Rider.Queue.Find("tx-commit").Info.ReceivedMessages);
    }
}
