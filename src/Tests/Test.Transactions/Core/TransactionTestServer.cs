using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Transactions;
using Horse.Server;

namespace Test.Transactions.Core;

/// <summary>
/// Running server with a rider for transaction tests.
/// The data folder is deleted on dispose unless KeepData is set (restart tests).
/// </summary>
internal sealed class TransactionTestServer : IAsyncDisposable
{
    public HorseRider Rider { get; }
    public HorseServer Server { get; }
    public int Port { get; }
    public string DataPath { get; }
    public ErrorCollector Errors { get; }
    public bool KeepData { get; set; }

    private readonly List<HorseClient> _clients = new();

    private TransactionTestServer(HorseRider rider, HorseServer server, int port, string dataPath, ErrorCollector errors)
    {
        Rider = rider;
        Server = server;
        Port = port;
        DataPath = dataPath;
        Errors = errors;
    }

    public static string NewDataPath() => $"tx-test-{Guid.NewGuid():N}";

    public static async Task<TransactionTestServer> Start(string dataPath = null)
    {
        dataPath ??= NewDataPath();
        ErrorCollector errors = new ErrorCollector();

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .AddErrorHandler(errors)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.AutoQueueCreation = true;
                q.UseMemoryQueues();
            })
            .Build();

        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = Random.Shared.Next(10000, 60000);
                HorseServerOptions options = HorseServerOptions.CreateDefault();
                options.Hosts[0].Port = port;
                options.PingInterval = 300;
                options.RequestTimeout = 300;

                HorseServer server = new HorseServer(options);
                server.UseRider(rider);
                await server.StartAsync();
                await Task.Delay(50);

                return new TransactionTestServer(rider, server, port, dataPath, errors);
            }
            catch
            {
                await Task.Delay(5);
            }
        }

        throw new InvalidOperationException("Test server could not be started");
    }

    public async Task<HorseClient> Connect(string clientId = null)
    {
        HorseClient client = new HorseClient();
        if (clientId != null)
            client.ClientId = clientId;

        await client.ConnectAsync($"horse://localhost:{Port}");
        await Wait.Until(() => client.IsConnected);

        lock (_clients)
            _clients.Add(client);

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_clients)
        {
            foreach (HorseClient client in _clients)
            {
                try
                {
                    client.Disconnect();
                }
                catch
                {
                }
            }
        }

        try
        {
            await Server.StopAsync();
        }
        catch
        {
        }

        if (KeepData)
            return;

        try
        {
            if (Directory.Exists(DataPath))
                Directory.Delete(DataPath, true);
        }
        catch
        {
        }
    }
}

internal static class Wait
{
    public static async Task<bool> Until(Func<bool> condition, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(20);
        }

        return condition();
    }
}

internal static class Raw
{
    /// <summary>
    /// Sends a transaction request without HorseTransaction state checks
    /// </summary>
    public static async Task<HorseResultCode> Send(HorseClient client, string name, ushort contentType, string transactionId)
    {
        HorseMessage message = new HorseMessage(MessageType.Transaction, name, contentType);
        message.AddHeader(HorseHeaders.TRANSACTION_ID, transactionId);
        HorseMessage response = await client.Request(message, CancellationToken.None);
        return (HorseResultCode) response.ContentType;
    }
}

/// <summary>
/// Endpoint that records the transactions it receives.
/// Instances are registered by name, so restored endpoints can be found by tests.
/// </summary>
public class RecordingEndpoint : IServerTransactionEndpoint
{
    public static ConcurrentDictionary<string, RecordingEndpoint> Instances { get; } = new();

    private int _count;

    public string Name { get; }
    public string InitParameter => Name;
    public int Count => Volatile.Read(ref _count);
    public bool Fail { get; set; }
    public bool Throw { get; set; }
    public int DelayMs { get; set; }
    public ConcurrentQueue<ServerTransaction> Received { get; } = new();

    public RecordingEndpoint(string name)
    {
        Name = name;
        Instances[name] = this;
    }

    public async Task<bool> Send(ServerTransaction transaction)
    {
        if (DelayMs > 0)
            await Task.Delay(DelayMs);

        if (Throw)
            throw new InvalidOperationException("endpoint failure");

        if (Fail)
            return false;

        Received.Enqueue(transaction);
        Interlocked.Increment(ref _count);
        return true;
    }
}

/// <summary>
/// Handler that records its calls
/// </summary>
public class TestTransactionHandler : IServerTransactionHandler
{
    public bool AllowCreate { get; set; } = true;
    public bool ThrowOnCommit { get; set; }
    public ConcurrentQueue<string> Calls { get; } = new();

    public Task<bool> CanCreate(ServerTransaction transaction)
    {
        Calls.Enqueue($"CanCreate:{transaction.Id}");
        return Task.FromResult(AllowCreate);
    }

    public Task Commit(ServerTransaction transaction)
    {
        Calls.Enqueue($"Commit:{transaction.Id}");
        if (ThrowOnCommit)
            throw new InvalidOperationException("handler failure");

        return Task.CompletedTask;
    }

    public Task Rollback(ServerTransaction transaction)
    {
        Calls.Enqueue($"Rollback:{transaction.Id}");
        return Task.CompletedTask;
    }

    public Task Timeout(ServerTransaction transaction)
    {
        Calls.Enqueue($"Timeout:{transaction.Id}");
        return Task.CompletedTask;
    }

    public Task SendFailed(ServerTransaction transaction, ServerTransactionStatus operation, IServerTransactionEndpoint endpoint)
    {
        Calls.Enqueue($"SendFailed:{operation}:{transaction.Id}");
        return Task.CompletedTask;
    }

    public Task Destroy(ServerTransactionContainer container)
    {
        Calls.Enqueue($"Destroy:{container.Name}");
        return Task.CompletedTask;
    }
}

internal class ErrorCollector : IErrorHandler
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        Messages.Enqueue($"{eventId}: {message} {exception?.Message}");
    }
}

/// <summary>
/// Single threaded synchronization context that never runs posted callbacks,
/// like a UI thread that is blocked.
/// </summary>
internal sealed class BlockedSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object state)
    {
    }

    public override void Send(SendOrPostCallback d, object state)
    {
    }
}
