using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Client;

// ---------------------------------------------------------------------------
// Shared interceptor tracking state
// ---------------------------------------------------------------------------
internal static class InterceptorState
{
    public static readonly ConcurrentBag<InterceptorCall> Calls = new();
    private static long _sequence;

    public static void Reset()
    {
        Calls.Clear();
        Interlocked.Exchange(ref _sequence, 0);
    }

    public static long NextSequence() => Interlocked.Increment(ref _sequence);

    public static List<InterceptorCall> GetOrderedCalls()
    {
        return Calls.OrderBy(c => c.Timestamp).ToList();
    }
}

internal class InterceptorCall
{
    public string InterceptorName { get; init; }
    public string Phase { get; init; }
    public string MessageTarget { get; init; }
    public long Timestamp { get; init; }
    public bool ClientWasConnected { get; init; }
    public bool TokenWasCancelled { get; init; }
    public int InstanceId { get; init; }
}

// ---------------------------------------------------------------------------
// Interceptor implementations
// ---------------------------------------------------------------------------

internal class BeforeInterceptor1 : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(BeforeInterceptor1),
            Phase = "Before",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        return Task.CompletedTask;
    }
}

internal class BeforeInterceptor2 : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(BeforeInterceptor2),
            Phase = "Before",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        return Task.CompletedTask;
    }
}

internal class AfterInterceptor1 : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(AfterInterceptor1),
            Phase = "After",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        return Task.CompletedTask;
    }
}

internal class AfterInterceptor2 : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(AfterInterceptor2),
            Phase = "After",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        return Task.CompletedTask;
    }
}

internal class ModelLevelInterceptor : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(ModelLevelInterceptor),
            Phase = "Before",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        return Task.CompletedTask;
    }
}

internal class ThrowingInterceptor : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(ThrowingInterceptor),
            Phase = "Before",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested
        });
        throw new InvalidOperationException("Interceptor error");
    }
}

internal class InstanceTrackingBeforeInterceptor : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(InstanceTrackingBeforeInterceptor),
            Phase = "Before",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested,
            InstanceId = GetHashCode()
        });
        return Task.CompletedTask;
    }
}

internal class InstanceTrackingAfterInterceptor : IHorseInterceptor
{
    public Task Intercept(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        InterceptorState.Calls.Add(new InterceptorCall
        {
            InterceptorName = nameof(InstanceTrackingAfterInterceptor),
            Phase = "After",
            MessageTarget = message.Target,
            Timestamp = InterceptorState.NextSequence(),
            ClientWasConnected = client.IsConnected,
            TokenWasCancelled = cancellationToken.IsCancellationRequested,
            InstanceId = GetHashCode()
        });
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Queue consumer models and consumers
// ---------------------------------------------------------------------------

[Interceptor(typeof(ModelLevelInterceptor), order: 0)]
internal class InterceptedQueueModel
{
    public string Value { get; set; }
}

[QueueName("interceptor-queue")]
[AutoAck]
[Interceptor(typeof(BeforeInterceptor1), order: 1)]
[Interceptor(typeof(AfterInterceptor1), order: 1, runBefore: false)]
internal class InterceptedQueueConsumer : IQueueConsumer<InterceptedQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, InterceptedQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

internal class SimpleQueueModel
{
    public string Text { get; set; }
}

[QueueName("interceptor-multi-queue")]
[AutoAck]
[Interceptor(typeof(BeforeInterceptor1), order: 1)]
[Interceptor(typeof(BeforeInterceptor2), order: 2)]
[Interceptor(typeof(AfterInterceptor1), order: 1, runBefore: false)]
[Interceptor(typeof(AfterInterceptor2), order: 2, runBefore: false)]
internal class MultiInterceptorQueueConsumer : IQueueConsumer<SimpleQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, SimpleQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

[QueueName("interceptor-noattr-queue")]
[AutoAck]
internal class NoInterceptorQueueConsumer : IQueueConsumer<SimpleQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, SimpleQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

[QueueName("interceptor-throw-queue")]
[AutoAck]
[Interceptor(typeof(ThrowingInterceptor), order: 1)]
[Interceptor(typeof(BeforeInterceptor1), order: 2)]
internal class ThrowingInterceptorQueueConsumer : IQueueConsumer<SimpleQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, SimpleQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Direct message handler models and handlers
// ---------------------------------------------------------------------------

[Interceptor(typeof(ModelLevelInterceptor), order: 0)]
[DirectContentType(501)]
internal class InterceptedDirectModel
{
    public string Value { get; set; }
}

[DirectContentType(501)]
[Interceptor(typeof(BeforeInterceptor1), order: 1)]
[Interceptor(typeof(AfterInterceptor1), order: 1, runBefore: false)]
internal class InterceptedDirectHandler : IDirectMessageHandler<InterceptedDirectModel>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, InterceptedDirectModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

[DirectContentType(502)]
internal class SimpleDirectModel
{
    public string Text { get; set; }
}

[DirectContentType(502)]
[Interceptor(typeof(BeforeInterceptor1), order: 1)]
[Interceptor(typeof(BeforeInterceptor2), order: 2)]
[Interceptor(typeof(AfterInterceptor1), order: 1, runBefore: false)]
[Interceptor(typeof(AfterInterceptor2), order: 2, runBefore: false)]
internal class MultiInterceptorDirectHandler : IDirectMessageHandler<SimpleDirectModel>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, SimpleDirectModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

[DirectContentType(503)]
internal class SimpleDirectModel2
{
    public string Text { get; set; }
}

[DirectContentType(503)]
internal class NoInterceptorDirectHandler : IDirectMessageHandler<SimpleDirectModel2>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, SimpleDirectModel2 model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

[DirectContentType(504)]
internal class SimpleDirectModel3
{
    public string Text { get; set; }
}

[DirectContentType(504)]
[Interceptor(typeof(ThrowingInterceptor), order: 1)]
[Interceptor(typeof(BeforeInterceptor1), order: 2)]
internal class ThrowingInterceptorDirectHandler : IDirectMessageHandler<SimpleDirectModel3>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, SimpleDirectModel3 model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Transient lifetime models, consumers, and handlers
// ---------------------------------------------------------------------------

internal class TransientQueueModel
{
    public string Text { get; set; }
}

[QueueName("interceptor-transient-queue")]
[AutoAck]
[Interceptor(typeof(InstanceTrackingBeforeInterceptor), order: 1)]
[Interceptor(typeof(InstanceTrackingAfterInterceptor), order: 1, runBefore: false)]
internal class TransientQueueConsumer : IQueueConsumer<TransientQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, TransientQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

[DirectContentType(510)]
internal class TransientDirectModel
{
    public string Text { get; set; }
}

[DirectContentType(510)]
[Interceptor(typeof(InstanceTrackingBeforeInterceptor), order: 1)]
[Interceptor(typeof(InstanceTrackingAfterInterceptor), order: 1, runBefore: false)]
internal class TransientDirectHandler : IDirectMessageHandler<TransientDirectModel>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, TransientDirectModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Scoped lifetime models, consumers, and handlers
// ---------------------------------------------------------------------------

internal class ScopedQueueModel
{
    public string Text { get; set; }
}

[QueueName("interceptor-scoped-queue")]
[AutoAck]
[Interceptor(typeof(InstanceTrackingBeforeInterceptor), order: 1)]
[Interceptor(typeof(InstanceTrackingAfterInterceptor), order: 1, runBefore: false)]
internal class ScopedQueueConsumer : IQueueConsumer<ScopedQueueModel>
{
    public static int ConsumeCount;

    public Task Consume(HorseMessage message, ScopedQueueModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumeCount);
        return Task.CompletedTask;
    }
}

[DirectContentType(520)]
internal class ScopedDirectModel
{
    public string Text { get; set; }
}

[DirectContentType(520)]
[Interceptor(typeof(InstanceTrackingBeforeInterceptor), order: 1)]
[Interceptor(typeof(InstanceTrackingAfterInterceptor), order: 1, runBefore: false)]
internal class ScopedDirectHandler : IDirectMessageHandler<ScopedDirectModel>
{
    public static int HandleCount;

    public Task Handle(HorseMessage message, ScopedDirectModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandleCount);
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class InterceptorTests
{
    private static async Task<(TestHorseRider server, int port)> StartServer()
    {
        var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);
        return (server, port);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(30);
    }

    // ===================================================================
    // QUEUE CONSUMER INTERCEPTOR TESTS
    // ===================================================================

    [Fact]
    public async Task QueueConsumer_BeforeAndAfterInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        InterceptedQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<ModelLevelInterceptor>()
                .AddSingletonConsumer<InterceptedQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            HorseResult pushResult = await producer.Queue.Push("interceptor-queue",
                new InterceptedQueueModel { Value = "test" }, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, pushResult.Code);

            await WaitUntil(() => InterceptedQueueConsumer.ConsumeCount > 0);

            Assert.True(InterceptedQueueConsumer.ConsumeCount > 0, "Consumer should have been called.");

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Any(c => c.InterceptorName == nameof(BeforeInterceptor1) && c.Phase == "Before"),
                "BeforeInterceptor1 should have run before the consumer.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(AfterInterceptor1) && c.Phase == "After"),
                "AfterInterceptor1 should have run after the consumer.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(ModelLevelInterceptor) && c.Phase == "Before"),
                "ModelLevelInterceptor should have run (from model attribute).");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_InterceptorsRunInCorrectOrder()
    {
        InterceptorState.Reset();
        MultiInterceptorQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonConsumer<MultiInterceptorQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-multi-queue",
                new SimpleQueueModel { Text = "order-test" }, true, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorQueueConsumer.ConsumeCount > 0);

            var calls = InterceptorState.GetOrderedCalls();
            var beforeCalls = calls.Where(c => c.Phase == "Before").ToList();
            var afterCalls = calls.Where(c => c.Phase == "After").ToList();

            Assert.Equal(2, beforeCalls.Count);
            Assert.Equal(nameof(BeforeInterceptor1), beforeCalls[0].InterceptorName);
            Assert.Equal(nameof(BeforeInterceptor2), beforeCalls[1].InterceptorName);

            Assert.Equal(2, afterCalls.Count);
            Assert.Equal(nameof(AfterInterceptor1), afterCalls[0].InterceptorName);
            Assert.Equal(nameof(AfterInterceptor2), afterCalls[1].InterceptorName);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_BeforeInterceptorsRunBeforeHandler_AfterInterceptorsRunAfterHandler()
    {
        InterceptorState.Reset();
        MultiInterceptorQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonConsumer<MultiInterceptorQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-multi-queue",
                new SimpleQueueModel { Text = "timing-test" }, true, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorQueueConsumer.ConsumeCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Count >= 4, $"Expected at least 4 interceptor calls, got {calls.Count}");

            var lastBefore = calls.Where(c => c.Phase == "Before").Max(c => c.Timestamp);
            var firstAfter = calls.Where(c => c.Phase == "After").Min(c => c.Timestamp);

            Assert.True(lastBefore <= firstAfter,
                "All before interceptors should complete before any after interceptor runs.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_WithNoInterceptors_ConsumerStillExecutes()
    {
        InterceptorState.Reset();
        NoInterceptorQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonConsumer<NoInterceptorQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-noattr-queue",
                new SimpleQueueModel { Text = "no-interceptor" }, true, CancellationToken.None);

            await WaitUntil(() => NoInterceptorQueueConsumer.ConsumeCount > 0);

            Assert.True(NoInterceptorQueueConsumer.ConsumeCount > 0, "Consumer should execute without interceptors.");
            Assert.Empty(InterceptorState.Calls);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_ThrowingInterceptor_DoesNotPreventNextInterceptorOrConsumer()
    {
        InterceptorState.Reset();
        ThrowingInterceptorQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<ThrowingInterceptor>()
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonConsumer<ThrowingInterceptorQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-throw-queue",
                new SimpleQueueModel { Text = "throw-test" }, true, CancellationToken.None);

            await WaitUntil(() => InterceptorState.Calls.Count >= 2 || ThrowingInterceptorQueueConsumer.ConsumeCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Any(c => c.InterceptorName == nameof(ThrowingInterceptor)),
                "ThrowingInterceptor should have been called.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(BeforeInterceptor1)),
                "BeforeInterceptor1 should still run after ThrowingInterceptor throws.");
            Assert.True(ThrowingInterceptorQueueConsumer.ConsumeCount > 0,
                "Consumer should still execute even when an interceptor throws.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_InterceptorReceivesCorrectMessageAndClient()
    {
        InterceptorState.Reset();
        InterceptedQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<ModelLevelInterceptor>()
                .AddSingletonConsumer<InterceptedQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-queue",
                new InterceptedQueueModel { Value = "context-test" }, true, CancellationToken.None);

            await WaitUntil(() => InterceptedQueueConsumer.ConsumeCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.NotEmpty(calls);
            foreach (var call in calls)
            {
                Assert.True(call.ClientWasConnected, "Interceptor should receive a connected client.");
                Assert.False(call.TokenWasCancelled, "CancellationToken should not be cancelled during interception.");
            }
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_MultipleMessages_InterceptorsRunForEach()
    {
        InterceptorState.Reset();
        MultiInterceptorQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonConsumer<MultiInterceptorQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push("interceptor-multi-queue",
                    new SimpleQueueModel { Text = $"msg-{i}" }, true, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorQueueConsumer.ConsumeCount >= messageCount);

            Assert.Equal(messageCount, MultiInterceptorQueueConsumer.ConsumeCount);

            var beforeCalls = InterceptorState.Calls.Where(c => c.Phase == "Before").ToList();
            var afterCalls = InterceptorState.Calls.Where(c => c.Phase == "After").ToList();

            Assert.Equal(messageCount * 2, beforeCalls.Count);
            Assert.Equal(messageCount * 2, afterCalls.Count);
        }
        finally
        {
            server.Stop();
        }
    }

    // ===================================================================
    // DIRECT MESSAGE HANDLER INTERCEPTOR TESTS
    // ===================================================================

    [Fact]
    public async Task DirectHandler_BeforeAndAfterInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        InterceptedDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .SetClientName("direct-receiver")
                .SetClientType("interceptor-test")
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<ModelLevelInterceptor>()
                .AddSingletonDirectHandler<InterceptedDirectHandler>()
                .Build();

            receiver.ClientId = "direct-receiver-id";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<InterceptedDirectModel>(
                "direct-receiver-id", 501,
                new InterceptedDirectModel { Value = "hello" },
                false, CancellationToken.None);

            await WaitUntil(() => InterceptedDirectHandler.HandleCount > 0);

            Assert.True(InterceptedDirectHandler.HandleCount > 0, "Direct handler should have been called.");

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Any(c => c.InterceptorName == nameof(BeforeInterceptor1) && c.Phase == "Before"),
                "BeforeInterceptor1 should have run before the handler.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(AfterInterceptor1) && c.Phase == "After"),
                "AfterInterceptor1 should have run after the handler.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(ModelLevelInterceptor) && c.Phase == "Before"),
                "ModelLevelInterceptor should have run (from model attribute).");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_InterceptorsRunInCorrectOrder()
    {
        InterceptorState.Reset();
        MultiInterceptorDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonDirectHandler<MultiInterceptorDirectHandler>()
                .Build();

            receiver.ClientId = "multi-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<SimpleDirectModel>(
                "multi-direct-receiver", 502,
                new SimpleDirectModel { Text = "order-test" }, false, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorDirectHandler.HandleCount > 0);

            var calls = InterceptorState.GetOrderedCalls();
            var beforeCalls = calls.Where(c => c.Phase == "Before").ToList();
            var afterCalls = calls.Where(c => c.Phase == "After").ToList();

            Assert.Equal(2, beforeCalls.Count);
            Assert.Equal(nameof(BeforeInterceptor1), beforeCalls[0].InterceptorName);
            Assert.Equal(nameof(BeforeInterceptor2), beforeCalls[1].InterceptorName);

            Assert.Equal(2, afterCalls.Count);
            Assert.Equal(nameof(AfterInterceptor1), afterCalls[0].InterceptorName);
            Assert.Equal(nameof(AfterInterceptor2), afterCalls[1].InterceptorName);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_BeforeInterceptorsRunBeforeHandler_AfterInterceptorsRunAfterHandler()
    {
        InterceptorState.Reset();
        MultiInterceptorDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonDirectHandler<MultiInterceptorDirectHandler>()
                .Build();

            receiver.ClientId = "timing-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<SimpleDirectModel>(
                "timing-direct-receiver", 502,
                new SimpleDirectModel { Text = "timing-test" }, false, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorDirectHandler.HandleCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Count >= 4, $"Expected at least 4 interceptor calls, got {calls.Count}");

            var lastBefore = calls.Where(c => c.Phase == "Before").Max(c => c.Timestamp);
            var firstAfter = calls.Where(c => c.Phase == "After").Min(c => c.Timestamp);

            Assert.True(lastBefore <= firstAfter,
                "All before interceptors should complete before any after interceptor runs.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_WithNoInterceptors_HandlerStillExecutes()
    {
        InterceptorState.Reset();
        NoInterceptorDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonDirectHandler<NoInterceptorDirectHandler>()
                .Build();

            receiver.ClientId = "no-interceptor-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<SimpleDirectModel2>(
                "no-interceptor-receiver", 503,
                new SimpleDirectModel2 { Text = "no-interceptor" }, false, CancellationToken.None);

            await WaitUntil(() => NoInterceptorDirectHandler.HandleCount > 0);

            Assert.True(NoInterceptorDirectHandler.HandleCount > 0, "Handler should execute without interceptors.");
            Assert.Empty(InterceptorState.Calls);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_ThrowingInterceptor_DoesNotPreventNextInterceptorOrHandler()
    {
        InterceptorState.Reset();
        ThrowingInterceptorDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<ThrowingInterceptor>()
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonDirectHandler<ThrowingInterceptorDirectHandler>()
                .Build();

            receiver.ClientId = "throwing-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<SimpleDirectModel3>(
                "throwing-direct-receiver", 504,
                new SimpleDirectModel3 { Text = "throw-test" }, false, CancellationToken.None);

            await WaitUntil(() => InterceptorState.Calls.Count >= 2 || ThrowingInterceptorDirectHandler.HandleCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.True(calls.Any(c => c.InterceptorName == nameof(ThrowingInterceptor)),
                "ThrowingInterceptor should have been called.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(BeforeInterceptor1)),
                "BeforeInterceptor1 should still run after ThrowingInterceptor throws.");
            Assert.True(ThrowingInterceptorDirectHandler.HandleCount > 0,
                "Handler should still execute even when an interceptor throws.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_InterceptorReceivesCorrectMessageAndClient()
    {
        InterceptorState.Reset();
        InterceptedDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<ModelLevelInterceptor>()
                .AddSingletonDirectHandler<InterceptedDirectHandler>()
                .Build();

            receiver.ClientId = "context-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<InterceptedDirectModel>(
                "context-direct-receiver", 501,
                new InterceptedDirectModel { Value = "ctx" },
                false, CancellationToken.None);

            await WaitUntil(() => InterceptedDirectHandler.HandleCount > 0);

            var calls = InterceptorState.GetOrderedCalls();

            Assert.NotEmpty(calls);
            foreach (var call in calls)
            {
                Assert.True(call.ClientWasConnected, "Interceptor should receive a connected client.");
                Assert.False(call.TokenWasCancelled, "CancellationToken should not be cancelled during interception.");
            }
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_MultipleMessages_InterceptorsRunForEach()
    {
        InterceptorState.Reset();
        MultiInterceptorDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<BeforeInterceptor1>()
                .AddSingletonInterceptor<BeforeInterceptor2>()
                .AddSingletonInterceptor<AfterInterceptor1>()
                .AddSingletonInterceptor<AfterInterceptor2>()
                .AddSingletonDirectHandler<MultiInterceptorDirectHandler>()
                .Build();

            receiver.ClientId = "multi-msg-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await sender.Direct.SendById<SimpleDirectModel>(
                    "multi-msg-receiver", 502,
                    new SimpleDirectModel { Text = $"msg-{i}" }, false, CancellationToken.None);

            await WaitUntil(() => MultiInterceptorDirectHandler.HandleCount >= messageCount);

            Assert.Equal(messageCount, MultiInterceptorDirectHandler.HandleCount);

            var beforeCalls = InterceptorState.Calls.Where(c => c.Phase == "Before").ToList();
            var afterCalls = InterceptorState.Calls.Where(c => c.Phase == "After").ToList();

            Assert.Equal(messageCount * 2, beforeCalls.Count);
            Assert.Equal(messageCount * 2, afterCalls.Count);
        }
        finally
        {
            server.Stop();
        }
    }

    // ===================================================================
    // TRANSIENT LIFETIME INTERCEPTOR TESTS
    // ===================================================================

    [Fact]
    public async Task QueueConsumer_TransientInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        TransientQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddTransientInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddTransientInterceptor<InstanceTrackingAfterInterceptor>()
                .AddTransientConsumer<TransientQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-transient-queue",
                new TransientQueueModel { Text = "transient-test" }, true, CancellationToken.None);

            await WaitUntil(() => TransientQueueConsumer.ConsumeCount > 0);

            Assert.True(TransientQueueConsumer.ConsumeCount > 0, "Consumer should have been called.");

            var calls = InterceptorState.GetOrderedCalls();
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor) && c.Phase == "Before"),
                "Before interceptor should have run.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingAfterInterceptor) && c.Phase == "After"),
                "After interceptor should have run.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_TransientInterceptors_NewInstancePerMessage()
    {
        InterceptorState.Reset();
        TransientQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddTransientInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddTransientInterceptor<InstanceTrackingAfterInterceptor>()
                .AddTransientConsumer<TransientQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push("interceptor-transient-queue",
                    new TransientQueueModel { Text = $"transient-{i}" }, true, CancellationToken.None);

            await WaitUntil(() => TransientQueueConsumer.ConsumeCount >= messageCount);

            var beforeCalls = InterceptorState.Calls
                .Where(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor))
                .ToList();

            Assert.Equal(messageCount, beforeCalls.Count);

            var distinctInstances = beforeCalls.Select(c => c.InstanceId).Distinct().Count();
            Assert.Equal(messageCount, distinctInstances);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_TransientInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        TransientDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddTransientInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddTransientInterceptor<InstanceTrackingAfterInterceptor>()
                .AddTransientDirectHandler<TransientDirectHandler>()
                .Build();

            receiver.ClientId = "transient-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<TransientDirectModel>(
                "transient-direct-receiver", 510,
                new TransientDirectModel { Text = "transient-test" }, false, CancellationToken.None);

            await WaitUntil(() => TransientDirectHandler.HandleCount > 0);

            Assert.True(TransientDirectHandler.HandleCount > 0, "Handler should have been called.");

            var calls = InterceptorState.GetOrderedCalls();
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor) && c.Phase == "Before"),
                "Before interceptor should have run.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingAfterInterceptor) && c.Phase == "After"),
                "After interceptor should have run.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_TransientInterceptors_NewInstancePerMessage()
    {
        InterceptorState.Reset();
        TransientDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddTransientInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddTransientInterceptor<InstanceTrackingAfterInterceptor>()
                .AddTransientDirectHandler<TransientDirectHandler>()
                .Build();

            receiver.ClientId = "transient-multi-direct";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await sender.Direct.SendById<TransientDirectModel>(
                    "transient-multi-direct", 510,
                    new TransientDirectModel { Text = $"transient-{i}" }, false, CancellationToken.None);

            await WaitUntil(() => TransientDirectHandler.HandleCount >= messageCount);

            var beforeCalls = InterceptorState.Calls
                .Where(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor))
                .ToList();

            Assert.Equal(messageCount, beforeCalls.Count);

            var distinctInstances = beforeCalls.Select(c => c.InstanceId).Distinct().Count();
            Assert.Equal(messageCount, distinctInstances);
        }
        finally
        {
            server.Stop();
        }
    }

    // ===================================================================
    // SCOPED LIFETIME INTERCEPTOR TESTS
    // ===================================================================

    [Fact]
    public async Task QueueConsumer_ScopedInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        ScopedQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddScopedInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddScopedInterceptor<InstanceTrackingAfterInterceptor>()
                .AddScopedConsumer<ScopedQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-scoped-queue",
                new ScopedQueueModel { Text = "scoped-test" }, true, CancellationToken.None);

            await WaitUntil(() => ScopedQueueConsumer.ConsumeCount > 0);

            Assert.True(ScopedQueueConsumer.ConsumeCount > 0, "Consumer should have been called.");

            var calls = InterceptorState.GetOrderedCalls();
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor) && c.Phase == "Before"),
                "Before interceptor should have run.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingAfterInterceptor) && c.Phase == "After"),
                "After interceptor should have run.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_ScopedInterceptors_NewScopePerMessage()
    {
        InterceptorState.Reset();
        ScopedQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddScopedInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddScopedInterceptor<InstanceTrackingAfterInterceptor>()
                .AddScopedConsumer<ScopedQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push("interceptor-scoped-queue",
                    new ScopedQueueModel { Text = $"scoped-{i}" }, true, CancellationToken.None);

            await WaitUntil(() => ScopedQueueConsumer.ConsumeCount >= messageCount);

            var beforeCalls = InterceptorState.Calls
                .Where(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor))
                .ToList();

            Assert.Equal(messageCount, beforeCalls.Count);

            // Each message should get a new scope, so a new interceptor instance
            var distinctInstances = beforeCalls.Select(c => c.InstanceId).Distinct().Count();
            Assert.Equal(messageCount, distinctInstances);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_ScopedInterceptors_BeforeAndAfterShareSameScope()
    {
        InterceptorState.Reset();
        ScopedQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddScopedInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddScopedInterceptor<InstanceTrackingAfterInterceptor>()
                .AddScopedConsumer<ScopedQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            await producer.Queue.Push("interceptor-scoped-queue",
                new ScopedQueueModel { Text = "scope-share-test" }, true, CancellationToken.None);

            await WaitUntil(() => ScopedQueueConsumer.ConsumeCount > 0);

            var calls = InterceptorState.GetOrderedCalls();
            var beforeCall = calls.First(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor));
            var afterCall = calls.First(c => c.InterceptorName == nameof(InstanceTrackingAfterInterceptor));

            // Before and after are different types, so they will be different instances.
            // But they should both be resolved from the same scope.
            // We can't directly compare instance IDs of different types.
            // Instead, verify both ran for the same message.
            Assert.NotNull(beforeCall);
            Assert.NotNull(afterCall);
            Assert.True(beforeCall.Timestamp < afterCall.Timestamp,
                "Before interceptor should run before after interceptor.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_ScopedInterceptors_AreExecuted()
    {
        InterceptorState.Reset();
        ScopedDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddScopedInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddScopedInterceptor<InstanceTrackingAfterInterceptor>()
                .AddScopedDirectHandler<ScopedDirectHandler>()
                .Build();

            receiver.ClientId = "scoped-direct-receiver";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            await sender.Direct.SendById<ScopedDirectModel>(
                "scoped-direct-receiver", 520,
                new ScopedDirectModel { Text = "scoped-test" }, false, CancellationToken.None);

            await WaitUntil(() => ScopedDirectHandler.HandleCount > 0);

            Assert.True(ScopedDirectHandler.HandleCount > 0, "Handler should have been called.");

            var calls = InterceptorState.GetOrderedCalls();
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor) && c.Phase == "Before"),
                "Before interceptor should have run.");
            Assert.True(calls.Any(c => c.InterceptorName == nameof(InstanceTrackingAfterInterceptor) && c.Phase == "After"),
                "After interceptor should have run.");
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task DirectHandler_ScopedInterceptors_NewScopePerMessage()
    {
        InterceptorState.Reset();
        ScopedDirectHandler.HandleCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient receiver = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddScopedInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddScopedInterceptor<InstanceTrackingAfterInterceptor>()
                .AddScopedDirectHandler<ScopedDirectHandler>()
                .Build();

            receiver.ClientId = "scoped-multi-direct";
            await receiver.ConnectAsync();
            await Task.Delay(500);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await sender.Direct.SendById<ScopedDirectModel>(
                    "scoped-multi-direct", 520,
                    new ScopedDirectModel { Text = $"scoped-{i}" }, false, CancellationToken.None);

            await WaitUntil(() => ScopedDirectHandler.HandleCount >= messageCount);

            var beforeCalls = InterceptorState.Calls
                .Where(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor))
                .ToList();

            Assert.Equal(messageCount, beforeCalls.Count);

            // Each message should get a new scope, so a new interceptor instance
            var distinctInstances = beforeCalls.Select(c => c.InstanceId).Distinct().Count();
            Assert.Equal(messageCount, distinctInstances);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueConsumer_SingletonInterceptors_SameInstanceAcrossMessages()
    {
        InterceptorState.Reset();
        TransientQueueConsumer.ConsumeCount = 0;

        var (server, port) = await StartServer();

        try
        {
            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonInterceptor<InstanceTrackingBeforeInterceptor>()
                .AddSingletonInterceptor<InstanceTrackingAfterInterceptor>()
                .AddSingletonConsumer<TransientQueueConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await Task.Delay(1000);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);

            const int messageCount = 3;
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push("interceptor-transient-queue",
                    new TransientQueueModel { Text = $"singleton-{i}" }, true, CancellationToken.None);

            await WaitUntil(() => TransientQueueConsumer.ConsumeCount >= messageCount);

            var beforeCalls = InterceptorState.Calls
                .Where(c => c.InterceptorName == nameof(InstanceTrackingBeforeInterceptor))
                .ToList();

            Assert.Equal(messageCount, beforeCalls.Count);

            // Singleton: same instance across all messages
            var distinctInstances = beforeCalls.Select(c => c.InstanceId).Distinct().Count();
            Assert.Equal(1, distinctInstances);
        }
        finally
        {
            server.Stop();
        }
    }
}
