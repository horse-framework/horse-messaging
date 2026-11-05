using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Transactions;
using Test.Transactions.Core;
using Xunit;

namespace Test.Transactions;

public class TransactionLifecycleTest
{
    private sealed record Endpoints(RecordingEndpoint Commit, RecordingEndpoint Rollback, RecordingEndpoint Timeout);

    private static Endpoints CreateContainer(TransactionTestServer server, string name, TimeSpan timeout, bool withTimeoutEndpoint = true, IServerTransactionHandler handler = null)
    {
        Endpoints endpoints = new Endpoints(
            new RecordingEndpoint($"{name}-commit-{Guid.NewGuid():N}"),
            new RecordingEndpoint($"{name}-rollback-{Guid.NewGuid():N}"),
            withTimeoutEndpoint ? new RecordingEndpoint($"{name}-timeout-{Guid.NewGuid():N}") : null);

        server.Rider.Transaction.CreateContainer(name, timeout, endpoints.Commit, endpoints.Rollback, endpoints.Timeout, handler);
        return endpoints;
    }

    [Fact]
    public async Task Commit_IsDeliveredOnce_AndTransactionIsRemoved()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        Assert.Equal(1, server.Rider.Transaction.Find("tx").Count);

        TransactionResult result = await transaction.Commit(CancellationToken.None);

        Assert.Equal(TransactionResult.Committed, result);
        Assert.Equal(TransactionStatus.Commit, transaction.Status);
        Assert.Equal(1, endpoints.Commit.Count);
        Assert.Equal(0, endpoints.Rollback.Count);
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);
        Assert.Equal(ServerTransactionStatus.Commit, endpoints.Commit.Received.Single().Status);
    }

    [Fact]
    public async Task RepeatedRequests_AfterCommit_ReturnNotFound_AndAreNotDelivered()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        await transaction.Commit(CancellationToken.None);

        Assert.Equal(HorseResultCode.NotFound, await Raw.Send(client, "tx", KnownContentTypes.TransactionCommit, transaction.Id));
        Assert.Equal(HorseResultCode.NotFound, await Raw.Send(client, "tx", KnownContentTypes.TransactionRollback, transaction.Id));

        Assert.Equal(1, endpoints.Commit.Count);
        Assert.Equal(0, endpoints.Rollback.Count);
    }

    [Fact]
    public async Task ConcurrentCommitAndRollback_OnlyOneIsDelivered()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        endpoints.Commit.DelayMs = 100;
        endpoints.Rollback.DelayMs = 100;
        HorseClient client = await server.Connect();

        for (int i = 0; i < 10; i++)
        {
            HorseTransaction transaction = new HorseTransaction(client, "tx");
            await transaction.Begin(CancellationToken.None);

            Task<HorseResultCode> commit = Raw.Send(client, "tx", KnownContentTypes.TransactionCommit, transaction.Id);
            Task<HorseResultCode> rollback = Raw.Send(client, "tx", KnownContentTypes.TransactionRollback, transaction.Id);
            HorseResultCode[] results = await Task.WhenAll(commit, rollback);

            Assert.Equal(1, results.Count(x => x == HorseResultCode.Ok));
            Assert.Equal(1, results.Count(x => x == HorseResultCode.NotFound));
        }

        Assert.Equal(10, endpoints.Commit.Count + endpoints.Rollback.Count);
    }

    [Fact]
    public async Task Rollback_IsDeliveredToRollbackEndpoint()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        TransactionResult result = await transaction.Rollback(CancellationToken.None);

        Assert.Equal(TransactionResult.RolledBack, result);
        Assert.Equal(TransactionStatus.Rollback, transaction.Status);
        Assert.Equal(0, endpoints.Commit.Count);
        Assert.Equal(1, endpoints.Rollback.Count);
    }

    [Fact]
    public async Task Timeout_IsDeliveredToTimeoutEndpoint_AndLateCommitIsNotFound()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        TestTransactionHandler handler = new TestTransactionHandler();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromMilliseconds(300), handler: handler);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.True(await Wait.Until(() => endpoints.Timeout.Count == 1, 3000));
        Assert.Equal(ServerTransactionStatus.Timeout, endpoints.Timeout.Received.Single().Status);
        Assert.Contains($"Timeout:{transaction.Id}", handler.Calls);
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);

        TransactionResult result = await transaction.Commit(CancellationToken.None);

        Assert.Equal(TransactionResult.NotFound, result);
        Assert.Equal(TransactionStatus.Ended, transaction.Status);
        Assert.Equal(0, endpoints.Commit.Count);
        Assert.Equal(0, endpoints.Rollback.Count);
    }

    [Fact]
    public async Task Timeout_WithoutTimeoutEndpoint_IsDeliveredToRollbackEndpoint()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromMilliseconds(300), withTimeoutEndpoint: false);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.True(await Wait.Until(() => endpoints.Rollback.Count == 1, 3000));
        Assert.Equal(ServerTransactionStatus.Timeout, endpoints.Rollback.Received.Single().Status);
    }

    [Fact]
    public async Task FailedCommitDelivery_IsRolledBack()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        TestTransactionHandler handler = new TestTransactionHandler();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30), handler: handler);
        endpoints.Commit.Throw = true;
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        TransactionResult result = await transaction.Commit(CancellationToken.None);

        Assert.Equal(TransactionResult.RolledBack, result);
        Assert.Equal(TransactionStatus.Rollback, transaction.Status);
        Assert.Equal(0, endpoints.Commit.Count);
        Assert.Equal(1, endpoints.Rollback.Count);
        Assert.Contains($"SendFailed:Commit:{transaction.Id}", handler.Calls);
        Assert.Contains($"Rollback:{transaction.Id}", handler.Calls);
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);
    }

    [Fact]
    public async Task FailedTimeoutDelivery_IsRolledBack()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromMilliseconds(300));
        endpoints.Timeout.Fail = true;
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.True(await Wait.Until(() => endpoints.Rollback.Count == 1, 3000));
        Assert.Equal(0, endpoints.Timeout.Count);
        Assert.Equal(ServerTransactionStatus.Rollback, endpoints.Rollback.Received.Single().Status);
    }

    [Fact]
    public async Task HandlerException_AfterDelivery_DoesNotChangeResult()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        TestTransactionHandler handler = new TestTransactionHandler { ThrowOnCommit = true };
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30), handler: handler);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        TransactionResult result = await transaction.Commit(CancellationToken.None);

        Assert.Equal(TransactionResult.Committed, result);
        Assert.Equal(1, endpoints.Commit.Count);

        //a rollback after the commit must not be delivered
        Assert.Equal(HorseResultCode.NotFound, await Raw.Send(client, "tx", KnownContentTypes.TransactionRollback, transaction.Id));
        Assert.Equal(0, endpoints.Rollback.Count);
    }

    [Fact]
    public async Task DefaultHandler_IsUsed_WhenContainerHasNoHandler()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        TestTransactionHandler handler = new TestTransactionHandler { AllowCreate = false };
        server.Rider.Transaction.DefaultHandler = handler;
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => transaction.Begin(CancellationToken.None));

        Assert.Equal(HorseResultCode.Unauthorized, exception.Code);
        Assert.Equal(TransactionStatus.None, transaction.Status);
        Assert.Contains($"CanCreate:{transaction.Id}", handler.Calls);
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);
        Assert.Equal(0, endpoints.Commit.Count + endpoints.Rollback.Count);
    }

    [Fact]
    public async Task OtherClient_CannotFinishTransaction()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient owner = await server.Connect();
        HorseClient other = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(owner, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.Equal(HorseResultCode.NotFound, await Raw.Send(other, "tx", KnownContentTypes.TransactionCommit, transaction.Id));
        Assert.Equal(HorseResultCode.NotFound, await Raw.Send(other, "tx", KnownContentTypes.TransactionRollback, transaction.Id));
        Assert.Equal(0, endpoints.Commit.Count + endpoints.Rollback.Count);

        Assert.Equal(TransactionResult.Committed, await transaction.Commit(CancellationToken.None));
        Assert.Equal(1, endpoints.Commit.Count);
    }

    [Fact]
    public async Task DuplicateBegin_IsRejected_AndFirstTransactionIsKept()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient first = await server.Connect();
        HorseClient second = await server.Connect();

        HorseTransaction transaction1 = new HorseTransaction(first, "tx", "same-id");
        transaction1.SetMessageContent("first"u8.ToArray());
        await transaction1.Begin(CancellationToken.None);

        HorseTransaction transaction2 = new HorseTransaction(second, "tx", "same-id");
        transaction2.SetMessageContent("second"u8.ToArray());
        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => transaction2.Begin(CancellationToken.None));

        Assert.Equal(HorseResultCode.Conflict, exception.Code);
        Assert.Equal(TransactionStatus.None, transaction2.Status);

        Assert.Equal(TransactionResult.Committed, await transaction1.Commit(CancellationToken.None));
        Assert.Equal("first", endpoints.Commit.Received.Single().Message.GetStringContent());
    }

    [Fact]
    public async Task Begin_OnUndefinedContainer_ThrowsNotFound()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "undefined");
        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => transaction.Begin(CancellationToken.None));

        Assert.Equal(HorseResultCode.NotFound, exception.Code);
    }

    [Fact]
    public async Task UnknownTransactionOperation_ReturnsBadRequest()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        Assert.Equal(HorseResultCode.BadRequest, await Raw.Send(client, "tx", 999, "id"));

        HorseMessage withoutId = new HorseMessage(MessageType.Transaction, "tx", KnownContentTypes.TransactionBegin);
        HorseMessage response = await client.Request(withoutId, CancellationToken.None);
        Assert.Equal(HorseResultCode.BadRequest, (HorseResultCode) response.ContentType);
    }

    [Fact]
    public async Task ClientDisconnect_RollsBackOpenTransactions()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();
        HorseClient other = await server.Connect();

        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);
        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);
        HorseTransaction otherTransaction = new HorseTransaction(other, "tx");
        await otherTransaction.Begin(CancellationToken.None);

        client.Disconnect();

        Assert.True(await Wait.Until(() => endpoints.Rollback.Count == 2, 3000));
        Assert.Equal(1, server.Rider.Transaction.Find("tx").Count);
        Assert.NotNull(server.Rider.Transaction.Find("tx").Find(otherTransaction.Id));
    }

    [Fact]
    public async Task Shutdown_RollsBackOpenTransactions_AndRejectsNewRequests()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        HorseTransaction open = new HorseTransaction(client, "tx");
        await open.Begin(CancellationToken.None);

        await server.Rider.Transaction.ShutdownAsync();

        Assert.Equal(1, endpoints.Rollback.Count);
        Assert.Equal(0, server.Rider.Transaction.Find("tx").Count);

        TransactionException exception = await Assert.ThrowsAsync<TransactionException>(() => new HorseTransaction(client, "tx").Begin(CancellationToken.None));
        Assert.Equal(HorseResultCode.Busy, exception.Code);

        Assert.Equal(TransactionResult.Unknown, await open.Commit(CancellationToken.None));
        Assert.Equal(0, endpoints.Commit.Count);
    }

    [Fact]
    public async Task ServerStop_RollsBackOpenTransactions_ByDisconnect()
    {
        TransactionTestServer server = await TransactionTestServer.Start();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30));
        HorseClient client = await server.Connect();

        await new HorseTransaction(client, "tx").Begin(CancellationToken.None);

        await server.Server.StopAsync();

        Assert.True(await Wait.Until(() => endpoints.Rollback.Count == 1, 3000));
        await server.DisposeAsync();
    }

    [Fact]
    public async Task DeleteContainer_RollsBackOpenTransactions()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        TestTransactionHandler handler = new TestTransactionHandler();
        Endpoints endpoints = CreateContainer(server, "tx", TimeSpan.FromSeconds(30), handler: handler);
        HorseClient client = await server.Connect();

        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.True(await server.Rider.Transaction.DeleteContainer("tx"));

        Assert.Equal(1, endpoints.Rollback.Count);
        Assert.Contains("Destroy:tx", handler.Calls);
        Assert.Null(server.Rider.Transaction.Find("tx"));
        Assert.Equal(TransactionResult.NotFound, await transaction.Commit(CancellationToken.None));
        Assert.False(await server.Rider.Transaction.DeleteContainer("tx"));
    }

    [Fact]
    public async Task CreateContainer_ValidatesArguments()
    {
        await using TransactionTestServer server = await TransactionTestServer.Start();
        RecordingEndpoint endpoint = new RecordingEndpoint($"validate-{Guid.NewGuid():N}");

        Assert.Throws<ArgumentOutOfRangeException>(() => server.Rider.Transaction.CreateContainer("tx", TimeSpan.Zero, endpoint, endpoint));
        Assert.Throws<ArgumentOutOfRangeException>(() => server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromDays(30), endpoint, endpoint));
        Assert.Throws<ArgumentNullException>(() => server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(1), null, endpoint));
        Assert.Null(server.Rider.Transaction.Find("tx"));

        server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(1), endpoint, endpoint);
        Assert.Throws<DuplicateNameException>(() => server.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(1), endpoint, endpoint));

        ServerTransactionContainer updated = server.Rider.Transaction.CreateOrUpdateContainer("tx", TimeSpan.FromSeconds(5), endpoint, endpoint);
        Assert.Equal(TimeSpan.FromSeconds(5), updated.Timeout);
        Assert.Same(updated, server.Rider.Transaction.Find("tx"));
    }
}
