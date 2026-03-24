using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Microsoft.Extensions.DependencyInjection;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Exceptions;

#region Retry Tracker

public class RetryTracker
{
    public int AttemptCount;
    public int ThrowCount;
    public readonly ConcurrentBag<string> ConsumedMessages = new();
    public bool ShouldThrow = true;
    public int StopThrowingAfterAttempt = int.MaxValue;
    public Type ExceptionTypeToThrow = typeof(InvalidOperationException);
    public string ExceptionMessage = "Retry test error";
}

public class RetryTrackerAccessor(RetryTracker tracker)
{
    public RetryTracker Tracker { get; } = tracker;
}

#endregion

#region Retry Consumer Models

[QueueName("retry-q")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
public class RetryModel
{
    public string Data { get; set; }
}

#endregion

#region Retry Consumers

/// <summary>
/// [Retry(3)] — 3 attempts, 50ms delay between retries.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(3, 50)]
public class Retry3Consumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        int attempt = Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow && attempt <= accessor.Tracker.StopThrowingAfterAttempt)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// [Retry(5, 10)] — 5 attempts, 10ms delay.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(5, 10)]
public class Retry5Consumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        int attempt = Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow && attempt <= accessor.Tracker.StopThrowingAfterAttempt)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// [Retry(1)] — Only 1 attempt, no actual retry.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(1)]
public class Retry1Consumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// [Retry(3)] with [PushExceptions] — retries exhausted then exception log pushed.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(3, 50)]
[PushExceptions<ExceptionLogModel>]
public class Retry3WithPushExceptionConsumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        int attempt = Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow && attempt <= accessor.Tracker.StopThrowingAfterAttempt)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// [Retry(3)] with IgnoreExceptions — ignored exception types are NOT retried, they propagate immediately.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.ExceptionType)]
[Retry(3, 50, IgnoreExceptions = new[] { typeof(CustomBusinessException) })]
public class Retry3IgnoreBusinessExceptionConsumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw (Exception)Activator.CreateInstance(accessor.Tracker.ExceptionTypeToThrow, accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// [Retry(3)] with [MoveOnError] — after retries exhausted, MoveOnError kicks in.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(3, 50)]
[MoveOnError("retry-error-q")]
public class Retry3WithMoveOnErrorConsumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer without retry attributes. Builder config drives retry behavior.
/// </summary>
public class BuilderRetryConsumer(RetryTrackerAccessor accessor) : IQueueConsumer<RetryModel>
{
    public Task Consume(HorseMessage message, RetryModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        int attempt = Interlocked.Increment(ref accessor.Tracker.AttemptCount);

        if (accessor.Tracker.ShouldThrow && attempt <= accessor.Tracker.StopThrowingAfterAttempt)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(model.Data);
        return Task.CompletedTask;
    }
}

#endregion

/// <summary>
/// Tests for the [Retry] attribute on queue consumers.
/// Verifies retry counts, delay behavior, IgnoreExceptions, and
/// interaction with [PushExceptions] and [MoveOnError].
/// Each scenario runs in both memory and persistent mode.
/// </summary>
public class RetryTest
{
    #region Helpers

    private static async Task<HorseClient> BuildRetryWorker<TConsumer>(int port, RetryTracker tracker,
        Action<QueueConfigBuilder> queueConfig = null)
        where TConsumer : class
    {
        RetryTrackerAccessor accessor = new(tracker);
        ServiceCollection services = new();
        services.AddSingleton(accessor);

        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{port}");
        builder.SetClientType("retry-worker");
        builder.AutoSubscribe(true);
        if (queueConfig == null)
            builder.AddScopedConsumer<TConsumer>();
        else
            builder.AddScopedConsumer<TConsumer>(queueConfig);

        HorseClient client = builder.Build();
        await client.ConnectAsync();
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task<HorseClient> ConnectRaw(int port)
    {
        HorseClient c = new();
        c.AutoAcknowledge = true;
        c.ResponseTimeout = TimeSpan.FromSeconds(10);
        await c.ConnectAsync($"horse://localhost:{port}");
        Assert.True(c.IsConnected);
        return c;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(50);
            elapsed += 50;
        }
    }

    private static async Task<QueueTestContext> CreateContext(string mode)
    {
        return await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.AutoQueueCreation = true;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            o.PutBack = PutBackDecision.No;
        });
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  1. Basic retry count
    // ═══════════════════════════════════════════════════════════════════

    #region Basic Retry Count

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UseRetry_ConfigBuilder_AllFail_ExactlyThreeAttempts(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<BuilderRetryConsumer>(ctx.Port, tracker, cfg =>
        {
            cfg.AutoNack(NegativeReason.Error);
            cfg.UseRetry(3, 50);
        });
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "builder-r3-test" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
        await Task.Delay(200);

        Assert.Equal(3, tracker.AttemptCount);
        Assert.Equal(3, tracker.ThrowCount);
        Assert.Empty(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_AllFail_ExactlyThreeAttempts(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry3Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "r3-test" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
        await Task.Delay(200);

        Assert.Equal(3, tracker.AttemptCount);
        Assert.Equal(3, tracker.ThrowCount);
        Assert.Empty(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry5_AllFail_ExactlyFiveAttempts(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry5Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "r5-test" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 5, 5_000);
        await Task.Delay(200);

        Assert.Equal(5, tracker.AttemptCount);
        Assert.Equal(5, tracker.ThrowCount);
        Assert.Empty(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry1_SingleAttemptOnly_NoActualRetry(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry1Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "r1-test" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 1, 5_000);
        await Task.Delay(200);

        Assert.Equal(1, tracker.AttemptCount);
        Assert.Equal(1, tracker.ThrowCount);
        Assert.Empty(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  2. Retry succeeds on Nth attempt
    // ═══════════════════════════════════════════════════════════════════

    #region Retry Succeeds

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_SucceedsOnSecondAttempt_TwoAttemptsTotal(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true, StopThrowingAfterAttempt = 1 };
        HorseClient worker = await BuildRetryWorker<Retry3Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "succeed-2nd" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
        await Task.Delay(200);

        Assert.Equal(2, tracker.AttemptCount);
        Assert.Equal(1, tracker.ThrowCount);
        Assert.Single(tracker.ConsumedMessages);
        Assert.Contains("succeed-2nd", tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_SucceedsOnThirdAttempt_ThreeAttemptsTotal(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true, StopThrowingAfterAttempt = 2 };
        HorseClient worker = await BuildRetryWorker<Retry3Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "succeed-3rd" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
        await Task.Delay(200);

        Assert.Equal(3, tracker.AttemptCount);
        Assert.Equal(2, tracker.ThrowCount);
        Assert.Single(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_SucceedsOnFirstAttempt_NoRetryNeeded(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = false };
        HorseClient worker = await BuildRetryWorker<Retry3Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "no-retry" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
        await Task.Delay(200);

        Assert.Equal(1, tracker.AttemptCount);
        Assert.Equal(0, tracker.ThrowCount);
        Assert.Single(tracker.ConsumedMessages);

        producer.Disconnect();
        worker.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  3. IgnoreExceptions — ignored exceptions skip retry, propagate immediately
    // ═══════════════════════════════════════════════════════════════════

    #region IgnoreExceptions

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_IgnoredExceptionType_ImmediatelyPropagates_NoRetry(string mode)
    {
        await using var ctx = await CreateContext(mode);

        // CustomBusinessException is in the IgnoreExceptions list
        RetryTracker tracker = new()
        {
            ShouldThrow = true,
            ExceptionTypeToThrow = typeof(CustomBusinessException),
            ExceptionMessage = "Business error"
        };
        HorseClient worker = await BuildRetryWorker<Retry3IgnoreBusinessExceptionConsumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "ignored-exc" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 1, 5_000);
        await Task.Delay(300);

        // Ignored exception should NOT be retried — only 1 attempt
        Assert.Equal(1, tracker.AttemptCount);
        Assert.Equal(1, tracker.ThrowCount);

        producer.Disconnect();
        worker.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_NonIgnoredExceptionType_RetriesNormally(string mode)
    {
        await using var ctx = await CreateContext(mode);

        // InvalidOperationException is NOT in the IgnoreExceptions list
        RetryTracker tracker = new()
        {
            ShouldThrow = true,
            ExceptionTypeToThrow = typeof(InvalidOperationException),
            ExceptionMessage = "Normal error"
        };
        HorseClient worker = await BuildRetryWorker<Retry3IgnoreBusinessExceptionConsumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "normal-retry" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
        await Task.Delay(200);

        // Non-ignored exception: retried 3 times
        Assert.Equal(3, tracker.AttemptCount);
        Assert.Equal(3, tracker.ThrowCount);

        producer.Disconnect();
        worker.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  4. Retry + PushExceptions interaction
    // ═══════════════════════════════════════════════════════════════════

    #region Retry With PushExceptions

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_Exhausted_PushExceptionFires(string mode)
    {
        await using var ctx = await CreateContext(mode);

        await ctx.Rider.Queue.Create("exception-log-q", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry3WithPushExceptionConsumer>(ctx.Port, tracker);
        await Task.Delay(500);

        ConcurrentBag<HorseMessage> logMsgs = new();
        HorseClient logConsumer = await ConnectRaw(ctx.Port);
        logConsumer.MessageReceived += (_, msg) => logMsgs.Add(msg);
        await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
        await Task.Delay(200);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "retry-push-exc" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
        await WaitUntil(() => logMsgs.Count >= 1, 5_000);

        Assert.Equal(3, tracker.ThrowCount);
        Assert.True(logMsgs.Count >= 1, $"Expected ≥1, got {logMsgs.Count}");

        // Verify the exception log content
        string content = logMsgs.First().GetStringContent();
        ExceptionLogModel logModel = JsonSerializer.Deserialize<ExceptionLogModel>(content);
        Assert.NotNull(logModel);
        Assert.Equal(typeof(InvalidOperationException).FullName, logModel.ExceptionType);

        producer.Disconnect();
        worker.Disconnect();
        logConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_Succeeds_NoPushExceptionFires(string mode)
    {
        await using var ctx = await CreateContext(mode);

        await ctx.Rider.Queue.Create("exception-log-q", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        // Fail first, succeed on 2nd attempt
        RetryTracker tracker = new() { ShouldThrow = true, StopThrowingAfterAttempt = 1 };
        HorseClient worker = await BuildRetryWorker<Retry3WithPushExceptionConsumer>(ctx.Port, tracker);
        await Task.Delay(500);

        ConcurrentBag<HorseMessage> logMsgs = new();
        HorseClient logConsumer = await ConnectRaw(ctx.Port);
        logConsumer.MessageReceived += (_, msg) => logMsgs.Add(msg);
        await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
        await Task.Delay(200);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "retry-ok" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
        await Task.Delay(500);

        Assert.Single(tracker.ConsumedMessages);
        Assert.Equal(1, tracker.ThrowCount);
        Assert.Empty(logMsgs); // No PushException since retry succeeded

        producer.Disconnect();
        worker.Disconnect();
        logConsumer.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  5. Retry + MoveOnError interaction
    // ═══════════════════════════════════════════════════════════════════

    #region Retry With MoveOnError

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_Exhausted_MoveOnErrorFires(string mode)
    {
        await using var ctx = await CreateContext(mode);

        await ctx.Rider.Queue.Create("retry-error-q", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry3WithMoveOnErrorConsumer>(ctx.Port, tracker);
        await Task.Delay(500);

        ConcurrentBag<HorseMessage> errorMsgs = new();
        HorseClient errorConsumer = await ConnectRaw(ctx.Port);
        errorConsumer.MessageReceived += (_, msg) => errorMsgs.Add(msg);
        await errorConsumer.Queue.Subscribe("retry-error-q", true, CancellationToken.None);
        await Task.Delay(200);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("retry-q", new RetryModel { Data = "retry-move" }, false, CancellationToken.None);

        await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
        await WaitUntil(() => errorMsgs.Count >= 1, 5_000);

        Assert.Equal(3, tracker.ThrowCount);
        Assert.True(errorMsgs.Count >= 1, $"Expected ≥1 error msg, got {errorMsgs.Count}");

        // Verify the moved message target is the error queue
        Assert.Equal("retry-error-q", errorMsgs.First().Target);

        producer.Disconnect();
        worker.Disconnect();
        errorConsumer.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  6. Multiple messages with retry
    // ═══════════════════════════════════════════════════════════════════

    #region Multiple Messages

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry3_MultipleMessages_EachRetriedIndependently(string mode)
    {
        await using var ctx = await CreateContext(mode);

        RetryTracker tracker = new() { ShouldThrow = true };
        HorseClient worker = await BuildRetryWorker<Retry3Consumer>(ctx.Port, tracker);
        await Task.Delay(500);

        HorseClient producer = await ConnectRaw(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        int msgCount = 3;
        for (int i = 0; i < msgCount; i++)
            await bus.Push("retry-q", new RetryModel { Data = $"multi-{i}" }, false, CancellationToken.None);

        // Each message retried 3 times → 9 total attempts
        await WaitUntil(() => tracker.ThrowCount >= msgCount * 3, 10_000);
        await Task.Delay(300);

        Assert.Equal(msgCount * 3, tracker.AttemptCount);
        Assert.Equal(msgCount * 3, tracker.ThrowCount);

        producer.Disconnect();
        worker.Disconnect();
    }

    #endregion
}
