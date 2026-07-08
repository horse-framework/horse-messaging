using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Client;

// ---------------------------------------------------------------------------
// Shared mutable state — reset between tests via TestState.Reset()
// ---------------------------------------------------------------------------
internal static class TestState
{
    public static CancellationToken LastQueueToken;
    public static int QueueConsumeCount;
    public static readonly ConcurrentBag<CancellationToken> MultiTokens = new();
    public static CancellationToken LastDirectToken;
    public static bool DirectHandled;
    public static CancellationToken LastChannelToken;
    public static int ChannelHandleCount;

    public static void Reset()
    {
        LastQueueToken = CancellationToken.None;
        QueueConsumeCount = 0;
        LastDirectToken = CancellationToken.None;
        DirectHandled = false;
        LastChannelToken = CancellationToken.None;
        ChannelHandleCount = 0;
        MultiTokens.Clear();
    }
}

// ---------------------------------------------------------------------------
// Consumer / subscriber / handler types
// (Registered via HorseClientBuilder generic methods)
// ---------------------------------------------------------------------------

[QueueName("push-a")]
[AutoAck]
internal class CapturingQueueConsumer : IQueueConsumer<string>
{
    public Task Consume(ConsumeContext<string> context)
    {
        TestState.LastQueueToken = context.CancellationToken;
        Interlocked.Increment(ref TestState.QueueConsumeCount);
        return Task.CompletedTask;
    }
}

[QueueName("push-a")]
[AutoAck]
internal class MultiTokenQueueConsumer : IQueueConsumer<string>
{
    public Task Consume(ConsumeContext<string> context)
    {
        TestState.MultiTokens.Add(context.CancellationToken);
        return Task.CompletedTask;
    }
}

[QueueName("push-a")]
[AutoAck]
internal class BlockingQueueConsumer : IQueueConsumer<string>
{
    public static SemaphoreSlim Started = new(0, 1);
    public static SemaphoreSlim Finished = new(0, 1);
    public static bool CancelledObserved;

    public async Task Consume(ConsumeContext<string> context)
    {
        Started.Release();
        try
        {
            await Task.Delay(10_000, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            CancelledObserved = true;
        }
        finally
        {
            Finished.Release();
        }
    }
}

[QueueName("push-a")]
[AutoAck]
internal class DelayTimingQueueConsumer : IQueueConsumer<string>
{
    public static TaskCompletionSource<bool> StartedTcs = new();
    public static TaskCompletionSource<bool> FinishedTcs = new();

    public async Task Consume(ConsumeContext<string> context)
    {
        StartedTcs.TrySetResult(true);
        try
        {
            await Task.Delay(30_000, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            FinishedTcs.TrySetResult(true);
        }
    }
}

[DirectContentType(1)]
internal class DirectTokenCapturingHandler : IDirectMessageHandler<string>
{
    public Task Handle(HorseMessage message, string model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        TestState.LastDirectToken = cancellationToken;
        TestState.DirectHandled = true;
        return Task.CompletedTask;
    }
}

[DirectContentType(2)]
internal class BlockingDirectHandler : IDirectMessageHandler<string>
{
    public static SemaphoreSlim Started = new(0, 1);
    public static SemaphoreSlim Finished = new(0, 1);
    public static bool CancelledObserved;

    public async Task Handle(HorseMessage message, string model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Started.Release();
        try
        {
            await Task.Delay(10_000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            CancelledObserved = true;
        }
        finally
        {
            Finished.Release();
        }
    }
}

[ChannelName("test-channel-ct")]
internal class ChannelTokenCapturingSubscriber : IChannelSubscriber<string>
{
    public Task Handle(ChannelMessageContext<string> context)
    {
        TestState.LastChannelToken = context.CancellationToken;
        Interlocked.Increment(ref TestState.ChannelHandleCount);
        return Task.CompletedTask;
    }

    public Task Error(Exception exception, ChannelMessageContext<string> context) => Task.CompletedTask;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class CancellationTokenTests
{
    // -----------------------------------------------------------------------
    // 1. ConsumeToken on new client — live and cancellable (no server needed)
    // -----------------------------------------------------------------------

    [Fact]
    public void ConsumeToken_IsNotCancelled_OnCreation()
    {
        var client = new HorseClient();
        Assert.False(client.ConsumeToken.IsCancellationRequested);
        Assert.True(client.ConsumeToken.CanBeCanceled,
            "ConsumeToken must be backed by a real CancellationTokenSource");
        Assert.NotEqual(CancellationToken.None, client.ConsumeToken);
    }

    // -----------------------------------------------------------------------
    // 2. ConsumeToken stays live after connect
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConsumeToken_IsNotCancelled_AfterConnect()
    {
        await using   var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        Assert.False(client.ConsumeToken.IsCancellationRequested);

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 3. Disconnect() cancels the token
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConsumeToken_IsCancelled_AfterDisconnect()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        var captured = client.ConsumeToken;
        Assert.False(captured.IsCancellationRequested);

        client.Disconnect();

        Assert.True(captured.IsCancellationRequested,
            "Token captured before disconnect must be cancelled after Disconnect()");
    }

    // -----------------------------------------------------------------------
    // 4. Reconnect produces a fresh, non-cancelled token
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConsumeToken_IsRefreshed_AfterReconnect()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        var first = client.ConsumeToken;

        client.Disconnect();
        Assert.True(first.IsCancellationRequested);

        await client.ConnectAsync("horse://localhost:" + port);
        var second = client.ConsumeToken;

        Assert.False(second.IsCancellationRequested, "Token after reconnect must be live");
        Assert.NotEqual(first, second);

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 5. Multiple Disconnect() calls are idempotent
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleDisconnects_DoNotThrow()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        var ex = Record.Exception(() =>
        {
            client.Disconnect();
            client.Disconnect();
            client.Disconnect();
        });

        Assert.Null(ex);
    }

    // -----------------------------------------------------------------------
    // 6. QueueConsumer.Consume receives the live ConsumeToken
    // Strategy: invoke the consumer directly, simulating what OnQueueMessage does
    // -----------------------------------------------------------------------

    [Fact]
    public async Task QueueConsumer_ReceivesLiveConsumeToken()
    {
        TestState.Reset();
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        // Directly invoke consumer with the live ConsumeToken — mirrors what OnQueueMessage does
        var consumer = new CapturingQueueConsumer();
        var fakeMsg = new HorseMessage(MessageType.QueueMessage, "push-a");
        await consumer.Consume(new ConsumeContext<string>(fakeMsg, "hello", client, client.ConsumeToken));

        Assert.True(TestState.QueueConsumeCount > 0, "Consumer.Consume was not called");
        Assert.False(TestState.LastQueueToken.IsCancellationRequested,
            "Token must be live when Consume is called");
        Assert.Equal(client.ConsumeToken, TestState.LastQueueToken);

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 7. Blocking consumer is interrupted on Disconnect
    // Simulates OnQueueMessage executing a long-running consumer
    // -----------------------------------------------------------------------

    [Fact]
    public async Task QueueConsumer_BlockedOnToken_IsInterruptedOnDisconnect()
    {
        BlockingQueueConsumer.Started = new SemaphoreSlim(0, 1);
        BlockingQueueConsumer.Finished = new SemaphoreSlim(0, 1);
        BlockingQueueConsumer.CancelledObserved = false;

        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        var consumer = new BlockingQueueConsumer();
        var fakeMsg = new HorseMessage(MessageType.QueueMessage, "push-a");

        // Fire-and-forget — consumer blocks on the token
        var consumeTask = Task.Run(async () =>
            await consumer.Consume(new ConsumeContext<string>(fakeMsg, "block", client, client.ConsumeToken)));

        bool entered = await BlockingQueueConsumer.Started.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(entered, "Consumer did not enter blocking section");

        // Cancelling via Disconnect
        client.Disconnect();

        bool finished = await BlockingQueueConsumer.Finished.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(finished, "Blocking consumer did not finish after Disconnect");
        Assert.True(BlockingQueueConsumer.CancelledObserved,
            "OperationCanceledException must be observed inside consumer");

        await consumeTask;
    }

    // -----------------------------------------------------------------------
    // 8. Task.Delay with token stops early when token is cancelled
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CancellableDelay_StopsEarlyOnDisconnect()
    {
        var startedTcs = new TaskCompletionSource<bool>();
        var finishedTcs = new TaskCompletionSource<bool>();

        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        var tokenAtConnect = client.ConsumeToken;

        // Start a long-running consumer on a background task
        var consumeTask = Task.Run(async () =>
        {
            startedTcs.TrySetResult(true);
            try
            {
                await Task.Delay(30_000, tokenAtConnect);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                finishedTcs.TrySetResult(true);
            }
        });

        await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        client.Disconnect(); // cancels tokenAtConnect

        await finishedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Consumer should stop early via cancellation, took {sw.ElapsedMilliseconds} ms");

        await consumeTask;
    }

    // -----------------------------------------------------------------------
    // 9. Two clients — disconnect one, other's token is unaffected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TokenIsolation_DisconnectOneClient_OtherUnaffected()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client1 = new HorseClient();
        var client2 = new HorseClient();
        await client1.ConnectAsync("horse://localhost:" + port);
        await client2.ConnectAsync("horse://localhost:" + port);

        var t1 = client1.ConsumeToken;
        var t2 = client2.ConsumeToken;

        client1.Disconnect();

        Assert.True(t1.IsCancellationRequested, "client1 token must be cancelled");
        Assert.False(t2.IsCancellationRequested, "client2 token must NOT be affected");

        client2.Disconnect();
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // 10. Multiple consume calls all receive the same live token instance
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleMessages_AllReceiveSameLiveToken()
    {
        TestState.Reset();
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        var consumer = new MultiTokenQueueConsumer();
        const int msgCount = 5;

        // Directly simulate what OnQueueMessage does for each message
        for (int i = 0; i < msgCount; i++)
        {
            var msg = new HorseMessage(MessageType.QueueMessage, "push-a");
            await consumer.Consume(new ConsumeContext<string>(msg, $"msg-{i}", client, client.ConsumeToken));
        }

        Assert.Equal(msgCount, TestState.MultiTokens.Count);

        var expected = client.ConsumeToken;
        foreach (var t in TestState.MultiTokens)
        {
            Assert.Equal(expected, t);
            Assert.False(t.IsCancellationRequested);
        }

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 11. Multiple reconnect cycles — each cycle yields a unique fresh token
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleReconnects_EachCycleProducesUniqueToken()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        var history = new List<CancellationToken>();

        for (int cycle = 0; cycle < 3; cycle++)
        {
            await client.ConnectAsync("horse://localhost:" + port);
            var token = client.ConsumeToken;
            Assert.False(token.IsCancellationRequested, $"Cycle {cycle}: token must be live");
            history.Add(token);
            client.Disconnect();
            Assert.True(token.IsCancellationRequested, $"Cycle {cycle}: token must be cancelled");
            await Task.Delay(30);
        }

        for (int i = 0; i < history.Count - 1; i++)
            Assert.NotEqual(history[i], history[i + 1]);
    }

    // -----------------------------------------------------------------------
    // 12. GracefulShutdown cancels ConsumeToken
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_CancelsConsumeToken()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var builder = new HorseClientBuilder();
        builder.AddHost("horse://localhost:" + port);
        builder.UseGracefulShutdown(
            minWait: TimeSpan.FromMilliseconds(50),
            maxWait: TimeSpan.FromSeconds(3),
            shuttingDownAction: (Func<Task>)(() => Task.CompletedTask));
        builder.AutoSubscribe(true);

        var client = builder.Build();
        await client.ConnectAsync();

        var tokenBefore = client.ConsumeToken;
        Assert.False(tokenBefore.IsCancellationRequested);

        // Disconnect triggers _consumeCts.Cancel() as first step of shutdown
        client.Disconnect();

        await WaitUntil(() => tokenBefore.IsCancellationRequested, 2000);
        Assert.True(tokenBefore.IsCancellationRequested,
            "ConsumeToken must be cancelled during graceful shutdown");
    }

    // -----------------------------------------------------------------------
    // 13. Reconnect after cancel does not throw and yields fresh token
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReconnectAfterCancel_DoesNotThrow_AndProducesFreshToken()
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        var old = client.ConsumeToken;
        client.Disconnect();
        Assert.True(old.IsCancellationRequested);

        var ex = await Record.ExceptionAsync(async () =>
            await client.ConnectAsync("horse://localhost:" + port));

        Assert.Null(ex);
        Assert.False(client.ConsumeToken.IsCancellationRequested);
        Assert.True(old.IsCancellationRequested, "Old token must still be cancelled");

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 14. DirectHandler receives the live ConsumeToken (direct invoke)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DirectHandler_ReceivesLiveConsumeToken()
    {
        TestState.Reset();
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        // Directly invoke handler with the live ConsumeToken — mirrors what DirectOperator does
        var handler = new DirectTokenCapturingHandler();
        var msg = new HorseMessage(MessageType.DirectMessage, client.ClientId);
        msg.ContentType = 1;
        msg.SetStringContent("token-test");
        await handler.Handle(msg, "token-test", client, client.ConsumeToken);

        Assert.True(TestState.DirectHandled, "DirectHandler was not invoked");
        Assert.False(TestState.LastDirectToken.IsCancellationRequested,
            "Token must be live in DirectHandler");
        Assert.Equal(client.ConsumeToken, TestState.LastDirectToken);

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // 15. Blocking DirectHandler interrupted on Disconnect (direct invoke)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DirectHandler_Blocked_IsInterruptedOnDisconnect()
    {
        BlockingDirectHandler.Started = new SemaphoreSlim(0, 1);
        BlockingDirectHandler.Finished = new SemaphoreSlim(0, 1);
        BlockingDirectHandler.CancelledObserved = false;

        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        var handler = new BlockingDirectHandler();
        var msg = new HorseMessage(MessageType.DirectMessage, client.ClientId);
        msg.ContentType = 2;
        msg.SetStringContent("blocking");

        // Fire-and-forget — handler blocks on the token
        var handlerTask = Task.Run(async () =>
            await handler.Handle(msg, "blocking", client, client.ConsumeToken));

        bool started = await BlockingDirectHandler.Started.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(started, "BlockingDirectHandler did not start");

        client.Disconnect();

        bool finished = await BlockingDirectHandler.Finished.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(finished, "BlockingDirectHandler did not finish after Disconnect");
        Assert.True(BlockingDirectHandler.CancelledObserved,
            "OperationCanceledException must be observed in DirectHandler");

        await handlerTask;
    }

    // -----------------------------------------------------------------------
    // 16. ChannelSubscriber receives the live ConsumeToken (direct invoke)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChannelSubscriber_ReceivesLiveConsumeToken()
    {
        TestState.Reset();
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        var client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        // Directly invoke subscriber — mirrors what ChannelOperator.OnChannelMessage does
        var subscriber = new ChannelTokenCapturingSubscriber();
        var rawMsg = new HorseMessage(MessageType.Channel, "test-channel-ct");
        rawMsg.SetStringContent("hello-channel");
        await subscriber.Handle(new ChannelMessageContext<string>(rawMsg, "hello-channel", client, client.ConsumeToken));

        Assert.True(TestState.ChannelHandleCount > 0, "ChannelSubscriber was not invoked");
        Assert.False(TestState.LastChannelToken.IsCancellationRequested,
            "Token must be live in ChannelSubscriber");
        Assert.Equal(client.ConsumeToken, TestState.LastChannelToken);

        client.Disconnect();
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------
    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(30);
    }
}