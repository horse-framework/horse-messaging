using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Server.Transactions;
using Test.Transactions.Core;
using Xunit;

namespace Test.Transactions;

public class TransactionPersistenceTest
{
    [Fact]
    public async Task QueueEndpointContainer_IsRestored_WhenQueueDidNotExist()
    {
        string dataPath = TransactionTestServer.NewDataPath();

        await using (TransactionTestServer first = await TransactionTestServer.Start(dataPath))
        {
            first.KeepData = true;
            first.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
                new QueueTransactionEndpoint(first.Rider.Queue, "restore-commit"),
                new QueueTransactionEndpoint(first.Rider.Queue, "restore-rollback"),
                new QueueTransactionEndpoint(first.Rider.Queue, "restore-timeout"));
        }

        await using TransactionTestServer second = await TransactionTestServer.Start(dataPath);
        ServerTransactionContainer container = second.Rider.Transaction.Find("tx");

        Assert.NotNull(container);
        Assert.Equal(TimeSpan.FromSeconds(30), container.Timeout);
        Assert.Equal("restore-commit", container.CommitEndpoint.InitParameter);
        Assert.Equal("restore-rollback", container.RollbackEndpoint.InitParameter);
        Assert.Equal("restore-timeout", container.TimeoutEndpoint.InitParameter);

        HorseClient client = await second.Connect();
        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);

        Assert.Equal(TransactionResult.Committed, await transaction.Commit(CancellationToken.None));
        Assert.NotNull(second.Rider.Queue.Find("restore-commit"));
    }

    [Fact]
    public async Task QueueEndpointContainer_IsRestored_WhenQueueExists()
    {
        string dataPath = TransactionTestServer.NewDataPath();

        await using (TransactionTestServer first = await TransactionTestServer.Start(dataPath))
        {
            first.KeepData = true;
            await first.Rider.Queue.Create("existing-commit");
            await first.Rider.Queue.Create("existing-rollback");

            first.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
                new QueueTransactionEndpoint(first.Rider.Queue.Find("existing-commit")),
                new QueueTransactionEndpoint(first.Rider.Queue, "existing-rollback"));
        }

        await using TransactionTestServer second = await TransactionTestServer.Start(dataPath);
        Assert.NotNull(second.Rider.Queue.Find("existing-commit"));
        Assert.NotNull(second.Rider.Transaction.Find("tx"));
        Assert.Null(second.Rider.Transaction.Find("tx").TimeoutEndpoint);

        HorseClient client = await second.Connect();

        HorseTransaction committed = new HorseTransaction(client, "tx");
        await committed.Begin(CancellationToken.None);
        Assert.Equal(TransactionResult.Committed, await committed.Commit(CancellationToken.None));

        HorseTransaction rolledBack = new HorseTransaction(client, "tx");
        await rolledBack.Begin(CancellationToken.None);
        Assert.Equal(TransactionResult.RolledBack, await rolledBack.Rollback(CancellationToken.None));
    }

    [Fact]
    public async Task CustomEndpointAndHandlerTypes_AreRestored()
    {
        string dataPath = TransactionTestServer.NewDataPath();
        string commitName = $"custom-commit-{Guid.NewGuid():N}";
        string rollbackName = $"custom-rollback-{Guid.NewGuid():N}";

        await using (TransactionTestServer first = await TransactionTestServer.Start(dataPath))
        {
            first.KeepData = true;
            first.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(10),
                new RecordingEndpoint(commitName),
                new RecordingEndpoint(rollbackName),
                null,
                new TestTransactionHandler());
        }

        RecordingEndpoint.Instances.TryRemove(commitName, out _);

        await using TransactionTestServer second = await TransactionTestServer.Start(dataPath);
        ServerTransactionContainer container = second.Rider.Transaction.Find("tx");

        Assert.NotNull(container);
        Assert.IsType<TestTransactionHandler>(container.Handler);
        Assert.IsType<RecordingEndpoint>(container.CommitEndpoint);
        Assert.Equal(commitName, container.CommitEndpoint.InitParameter);
        Assert.Null(container.TimeoutEndpoint);

        HorseClient client = await second.Connect();
        HorseTransaction transaction = new HorseTransaction(client, "tx");
        await transaction.Begin(CancellationToken.None);
        Assert.Equal(TransactionResult.Committed, await transaction.Commit(CancellationToken.None));

        Assert.True(RecordingEndpoint.Instances.TryGetValue(commitName, out RecordingEndpoint restored));
        Assert.Equal(1, restored.Count);
    }

    [Fact]
    public async Task CreateOrUpdate_OnRestoredContainer_UpdatesDefinition()
    {
        string dataPath = TransactionTestServer.NewDataPath();

        await using (TransactionTestServer first = await TransactionTestServer.Start(dataPath))
        {
            first.KeepData = true;
            first.Rider.Transaction.CreateOrUpdateContainer("tx", TimeSpan.FromSeconds(30),
                new QueueTransactionEndpoint(first.Rider.Queue, "old-commit"),
                new QueueTransactionEndpoint(first.Rider.Queue, "old-rollback"));
        }

        await using (TransactionTestServer second = await TransactionTestServer.Start(dataPath))
        {
            second.KeepData = true;
            ServerTransactionContainer container = second.Rider.Transaction.CreateOrUpdateContainer("tx", TimeSpan.FromSeconds(10),
                new QueueTransactionEndpoint(second.Rider.Queue, "new-commit"),
                new QueueTransactionEndpoint(second.Rider.Queue, "new-rollback"));

            Assert.Equal("new-commit", container.CommitEndpoint.InitParameter);
        }

        await using TransactionTestServer third = await TransactionTestServer.Start(dataPath);
        ServerTransactionContainer restored = third.Rider.Transaction.Find("tx");

        Assert.Equal(TimeSpan.FromSeconds(10), restored.Timeout);
        Assert.Equal("new-commit", restored.CommitEndpoint.InitParameter);
        Assert.Single(third.Rider.Transaction.GetContainers());
    }

    [Fact]
    public async Task CorruptFile_DoesNotStopServer()
    {
        string dataPath = TransactionTestServer.NewDataPath();
        Directory.CreateDirectory(dataPath);
        await File.WriteAllTextAsync(Path.Combine(dataPath, "transactions.json"), "{ this is not json");

        await using TransactionTestServer server = await TransactionTestServer.Start(dataPath);

        Assert.Empty(server.Rider.Transaction.GetContainers());
        Assert.Contains(server.Errors.Messages, x => x.Contains("transactions.json"));
    }

    [Fact]
    public async Task ContainerThatCannotBeLoaded_IsSkipped_AndKeptInFile()
    {
        string dataPath = TransactionTestServer.NewDataPath();
        Directory.CreateDirectory(dataPath);

        string json = """
                      [
                        {
                          "Name": "broken",
                          "Timeout": 30000,
                          "CommitType": "Missing.Namespace.MissingEndpoint, MissingAssembly",
                          "RollbackType": "Missing.Namespace.MissingEndpoint, MissingAssembly"
                        },
                        {
                          "Name": "valid",
                          "Timeout": 30000,
                          "CommitType": "Horse.Messaging.Server.Transactions.QueueTransactionEndpoint, Horse.Messaging.Server",
                          "CommitParam": "valid-commit",
                          "RollbackType": "Horse.Messaging.Server.Transactions.QueueTransactionEndpoint, Horse.Messaging.Server",
                          "RollbackParam": "valid-rollback"
                        }
                      ]
                      """;

        string filename = Path.Combine(dataPath, "transactions.json");
        await File.WriteAllTextAsync(filename, json);

        await using TransactionTestServer server = await TransactionTestServer.Start(dataPath);

        Assert.Null(server.Rider.Transaction.Find("broken"));
        Assert.NotNull(server.Rider.Transaction.Find("valid"));
        Assert.Contains(server.Errors.Messages, x => x.Contains("broken"));

        //saving another container keeps the entry that could not be loaded
        RecordingEndpoint endpoint = new RecordingEndpoint($"other-{Guid.NewGuid():N}");
        server.Rider.Transaction.CreateContainer("other", TimeSpan.FromSeconds(30), endpoint, endpoint);

        string saved = await File.ReadAllTextAsync(filename);
        Assert.Contains("broken", saved);
        Assert.Contains("valid", saved);
        Assert.Contains("other", saved);
    }

    [Fact]
    public async Task DeletedContainer_IsRemovedFromFile()
    {
        string dataPath = TransactionTestServer.NewDataPath();

        await using (TransactionTestServer first = await TransactionTestServer.Start(dataPath))
        {
            first.KeepData = true;
            first.Rider.Transaction.CreateContainer("tx", TimeSpan.FromSeconds(30),
                new QueueTransactionEndpoint(first.Rider.Queue, "delete-commit"),
                new QueueTransactionEndpoint(first.Rider.Queue, "delete-rollback"));

            Assert.True(await first.Rider.Transaction.DeleteContainer("tx"));
        }

        await using TransactionTestServer second = await TransactionTestServer.Start(dataPath);
        Assert.Null(second.Rider.Transaction.Find("tx"));
    }
}
