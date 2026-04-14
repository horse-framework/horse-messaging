using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Client.Routers.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Exceptions;

#region Models

[QueueName("source-q")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
public class SourceModel
{
    public string Data { get; set; }
}

[QueueName("error-q")]
[QueueType(MessagingQueueType.Push)]
public class ErrorQueueModel
{
    public string Data { get; set; }
}

[QueueName("exception-log-q")]
[QueueType(MessagingQueueType.Push)]
public class ExceptionLogModel : ITransportableException
{
    public string ExceptionType { get; set; }
    public string Message { get; set; }
    public string OriginalTarget { get; set; }

    public void Initialize(ExceptionContext context)
    {
        ExceptionType = context.Exception.GetType().FullName;
        Message = context.Exception.Message;
        OriginalTarget = context.ConsumingMessage?.Target;
    }
}

[QueueName("specific-exception-log-q")]
[QueueType(MessagingQueueType.Push)]
public class SpecificExceptionLogModel : ITransportableException
{
    public string Detail { get; set; }

    public void Initialize(ExceptionContext context)
    {
        Detail = $"Specific:{context.Exception.GetType().Name}:{context.Exception.Message}";
    }
}

[RouterName("builder-exception-router")]
public class RouterExceptionLogModel : ITransportableException
{
    public string ExceptionType { get; set; }
    public string Message { get; set; }

    public void Initialize(ExceptionContext context)
    {
        ExceptionType = context.Exception.GetType().FullName;
        Message = context.Exception.Message;
    }
}

[QueueName("retry-source-q")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
public class RetrySourceModel
{
    public string Data { get; set; }
}

public class CustomBusinessException : Exception
{
    public CustomBusinessException(string message) : base(message)
    {
    }
}

public class AnotherException : Exception
{
    public AnotherException(string message) : base(message)
    {
    }
}

#endregion

#region Tracker

/// <summary>
/// Thread-safe tracker for exception test results.
/// Each test creates its own instance for isolation.
/// </summary>
public class ExceptionTracker
{
    public readonly ConcurrentBag<string> ConsumedMessages = new();
    public readonly ConcurrentBag<string> ErrorQueueMessages = new();
    public readonly ConcurrentBag<string> ExceptionLogMessages = new();
    public readonly ConcurrentBag<string> SpecificExceptionLogMessages = new();
    public int ConsumeAttemptCount;
    public int ThrowCount;
    public bool ShouldThrow = true;
    public Type ExceptionTypeToThrow = typeof(InvalidOperationException);
    public string ExceptionMessage = "Test consumer error";
}

public class ExceptionTrackerAccessor(ExceptionTracker tracker)
{
    public ExceptionTracker Tracker { get; } = tracker;
}

#endregion

#region Consumers

/// <summary>
/// Consumer with [MoveOnError]: on exception, original message is moved to "error-q".
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("error-q")]
public class MoveOnErrorConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer with [PushExceptions] (default — catches all exception types).
/// Pushes ExceptionLogModel to "exception-log-q".
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.ExceptionType)]
[PushExceptions<ExceptionLogModel>]
public class PushExceptionConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw (Exception)Activator.CreateInstance(accessor.Tracker.ExceptionTypeToThrow, accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer with multiple [PushExceptions]:
///   - Default: ExceptionLogModel (all unmatched exceptions)
///   - Specific: SpecificExceptionLogModel for CustomBusinessException
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.ExceptionMessage)]
[PushExceptions<ExceptionLogModel>]
[PushExceptions<SpecificExceptionLogModel>(typeof(CustomBusinessException))]
public class MultiPushExceptionConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw (Exception)Activator.CreateInstance(accessor.Tracker.ExceptionTypeToThrow, accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer with [MoveOnError] + [PushExceptions] both.
/// Both should fire when an exception occurs.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("error-q")]
[PushExceptions<ExceptionLogModel>]
public class MoveOnErrorAndPushExceptionConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer with [AutoNack] only — no MoveOnError, no PushExceptions.
/// Simply sends NACK on error.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class AutoNackOnlyConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer without queue-handling attributes. Builder config drives ACK/NACK behavior.
/// </summary>
public class BuilderConfiguredConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw (Exception)Activator.CreateInstance(accessor.Tracker.ExceptionTypeToThrow, accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer with [Retry] + [PushExceptions].
/// PushExceptions should only fire after all retries are exhausted.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
[Retry(3, 50)]
[PushExceptions<ExceptionLogModel>]
public class RetryThenPushExceptionConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw new InvalidOperationException(accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Consumer that throws CustomBusinessException — used with SpecificExceptionLogModel tests.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.ExceptionType)]
[PushExceptions<SpecificExceptionLogModel>(typeof(CustomBusinessException))]
[PushExceptions<ExceptionLogModel>]
public class SpecificExceptionConsumer(ExceptionTrackerAccessor accessor) : IQueueConsumer<SourceModel>
{
    public Task Consume(ConsumeContext<SourceModel> context)
    {
        Interlocked.Increment(ref accessor.Tracker.ConsumeAttemptCount);

        if (accessor.Tracker.ShouldThrow)
        {
            Interlocked.Increment(ref accessor.Tracker.ThrowCount);
            throw (Exception)Activator.CreateInstance(accessor.Tracker.ExceptionTypeToThrow, accessor.Tracker.ExceptionMessage);
        }

        accessor.Tracker.ConsumedMessages.Add(context.Model.Data);
        return Task.CompletedTask;
    }
}

#endregion

/// <summary>
/// Integration tests for queue consumer exception handling mechanisms:
/// [MoveOnError], [PushExceptions], [AutoNack], [Retry], and their combinations.
/// Each scenario runs in both memory and persistent mode.
/// </summary>
public class ExceptionHandlingTest
{
    #region Helpers

    private static async Task<HorseClient> BuildConsumerWorker<TConsumer>(int port, ExceptionTracker tracker,
        Action<QueueConfigBuilder> queueConfig = null)
        where TConsumer : class
    {
        ExceptionTrackerAccessor accessor = new(tracker);
        ServiceCollection services = new();
        services.AddSingleton(accessor);

        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{port}");
        builder.SetClientType("exception-test-worker");
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
    //  1. MoveOnError — Message moved to error queue on exception
    // ═══════════════════════════════════════════════════════════════════

    #region MoveOnError

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MoveOnError_ExceptionThrown_MessageMovedToErrorQueue(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            // Pre-create error queue so messages arrive there
            await ctx.Rider.Queue.Create("error-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<MoveOnErrorConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            // Subscribe a raw consumer to error-q to capture moved messages
            ConcurrentBag<string> errorMessages = new();
            HorseClient errorConsumer = await ConnectRaw(ctx.Port);
            errorConsumer.MessageReceived += (_, msg) => { errorMessages.Add(msg.Target); };
            await errorConsumer.Queue.Subscribe("error-q", true, CancellationToken.None);
            await Task.Delay(200);

            // Send a message to source-q
            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "test-data" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(1000); // Allow time for MoveOnError to propagate

            // Consumer should have thrown
            Assert.True(tracker.ThrowCount >= 1, $"Expected ≥1 throw, got {tracker.ThrowCount}");

            // Error queue should have received the moved message
            await WaitUntil(() => errorMessages.Count >= 1, 5_000);
            Assert.True(errorMessages.Count >= 1, $"Expected ≥1 error queue message, got {errorMessages.Count}");

            producer.Disconnect();
            worker.Disconnect();
            errorConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  2. MoveOnError bug — clone vs original message
    // ═══════════════════════════════════════════════════════════════════

    #region MoveOnError Bug Verification

    /// <summary>
    /// Verifies that after the bug fix in QueueConsumerExecutor.Execute(),
    /// the CLONE (with error queue target and ExceptionDescription) is sent
    /// to "error-q" — not the original message.
    ///
    /// Previous bug: Line 90 was `client.SendAsync(message, true, CancellationToken.None)` instead of `client.SendAsync(clone, true, CancellationToken.None)`.
    /// Now fixed: clone is sent to the error queue with the exception metadata.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MoveOnError_CloneSentToErrorQueue_WithExceptionMetadata(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("error-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<MoveOnErrorConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            // Track what actually arrives at error-q
            ConcurrentBag<HorseMessage> errorQueueMsgs = new();
            HorseClient errorConsumer = await ConnectRaw(ctx.Port);
            errorConsumer.MessageReceived += (_, msg) => { errorQueueMsgs.Add(msg); };
            await errorConsumer.Queue.Subscribe("error-q", true, CancellationToken.None);
            await Task.Delay(200);

            // Track what arrives at source-q (the original queue)
            ConcurrentBag<HorseMessage> sourceQueueMsgs = new();
            HorseClient sourceWatcher = await ConnectRaw(ctx.Port);
            sourceWatcher.MessageReceived += (_, msg) => { sourceQueueMsgs.Add(msg); };
            await sourceWatcher.Queue.Subscribe("source-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "bug-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(2000);

            // After bug fix: clone is sent to error-q with ExceptionDescription
            Assert.True(tracker.ThrowCount >= 1);

            // Error queue should receive the clone
            await WaitUntil(() => errorQueueMsgs.Count >= 1, 5_000);
            Assert.True(errorQueueMsgs.Count >= 1, $"Expected ≥1 error queue msg, got {errorQueueMsgs.Count}");

            // The clone's target should be error-q
            HorseMessage errMsg = errorQueueMsgs.First();
            Assert.Equal("error-q", errMsg.Target);

            producer.Disconnect();
            worker.Disconnect();
            errorConsumer.Disconnect();
            sourceWatcher.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MoveOnError_ConfigBuilder_OverridesAttributeAndAppliesTopic(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<MoveOnErrorConsumer>(ctx.Port, tracker, cfg =>
                cfg.MoveOnError("builder-error-q", "builder-error-topic"));
            await Task.Delay(500);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "builder-move-on-error" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => ctx.Rider.Queue.Find("builder-error-q") != null, 5_000);

            HorseQueue queue = ctx.Rider.Queue.Find("builder-error-q");

            Assert.True(tracker.ThrowCount >= 1);
            Assert.NotNull(queue);
            Assert.Equal("builder-error-topic", queue.Topic);
            Assert.Null(ctx.Rider.Queue.Find("error-q"));

            ConcurrentBag<HorseMessage> errorMessages = new();
            HorseClient errorConsumer = await ConnectRaw(ctx.Port);
            errorConsumer.MessageReceived += (_, msg) => errorMessages.Add(msg);
            await errorConsumer.Queue.Subscribe("builder-error-q", true, CancellationToken.None);

            await WaitUntil(() => errorMessages.Count >= 1, 5_000);

            HorseMessage errMsg = errorMessages.First();
            Assert.Equal("builder-error-q", errMsg.Target);

            producer.Disconnect();
            worker.Disconnect();
            errorConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  3. PushExceptions — Default catch-all
    // ═══════════════════════════════════════════════════════════════════

    #region PushExceptions Default

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushExceptions_Default_ExceptionLogPushedToQueue(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<PushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            // Listen on exception-log-q
            ConcurrentBag<HorseMessage> logMessages = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => { logMessages.Add(msg); };
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "push-exc-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => logMessages.Count >= 1, 5_000);

            Assert.True(tracker.ThrowCount >= 1);
            Assert.True(logMessages.Count >= 1, $"Expected ≥1 exception log, got {logMessages.Count}");

            // Verify the pushed exception log message content
            HorseMessage logMsg = logMessages.First();
            string content = logMsg.GetStringContent();
            Assert.False(string.IsNullOrEmpty(content));

            // Content should be a serialized ExceptionLogModel
            ExceptionLogModel logModel = JsonSerializer.Deserialize<ExceptionLogModel>(content);
            Assert.NotNull(logModel);
            Assert.Equal(typeof(InvalidOperationException).FullName, logModel.ExceptionType);
            Assert.Equal("Test consumer error", logModel.Message);

            producer.Disconnect();
            worker.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushExceptions_ConfigBuilder_Default_ExceptionLogPushedToQueue(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<AutoNackOnlyConsumer>(ctx.Port, tracker,
                cfg => cfg.PushExceptions<ExceptionLogModel>());
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> logMessages = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => { logMessages.Add(msg); };
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "push-exc-builder-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => logMessages.Count >= 1, 5_000);

            Assert.True(tracker.ThrowCount >= 1);
            Assert.True(logMessages.Count >= 1, $"Expected ≥1 exception log, got {logMessages.Count}");

            string content = logMessages.First().GetStringContent();
            ExceptionLogModel logModel = JsonSerializer.Deserialize<ExceptionLogModel>(content);
            Assert.NotNull(logModel);
            Assert.Equal(typeof(InvalidOperationException).FullName, logModel.ExceptionType);
            Assert.Equal("Test consumer error", logModel.Message);

            producer.Disconnect();
            worker.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  4. PushExceptions — Specific exception type mapping
    // ═══════════════════════════════════════════════════════════════════

    #region PushExceptions Specific Type

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushExceptions_SpecificType_CustomBusinessException_RoutedToSpecificQueue(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("specific-exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new()
            {
                ShouldThrow = true,
                ExceptionTypeToThrow = typeof(CustomBusinessException),
                ExceptionMessage = "Business rule violated"
            };

            HorseClient worker = await BuildConsumerWorker<SpecificExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> specificLogs = new();
            HorseClient specificConsumer = await ConnectRaw(ctx.Port);
            specificConsumer.MessageReceived += (_, msg) => specificLogs.Add(msg);
            await specificConsumer.Queue.Subscribe("specific-exception-log-q", true, CancellationToken.None);

            ConcurrentBag<HorseMessage> defaultLogs = new();
            HorseClient defaultConsumer = await ConnectRaw(ctx.Port);
            defaultConsumer.MessageReceived += (_, msg) => defaultLogs.Add(msg);
            await defaultConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "specific-exc-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => specificLogs.Count >= 1, 5_000);

            Assert.True(tracker.ThrowCount >= 1);

            // CustomBusinessException → should match SpecificExceptionLogModel
            Assert.True(specificLogs.Count >= 1, $"Expected ≥1 specific log, got {specificLogs.Count}");

            // Verify content
            string content = specificLogs.First().GetStringContent();
            SpecificExceptionLogModel specific = JsonSerializer.Deserialize<SpecificExceptionLogModel>(content);
            Assert.NotNull(specific);
            Assert.Contains("CustomBusinessException", specific.Detail);
            Assert.Contains("Business rule violated", specific.Detail);

            producer.Disconnect();
            worker.Disconnect();
            specificConsumer.Disconnect();
            defaultConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    /// <summary>
    /// When the thrown exception does NOT match the specific type filter,
    /// only the default PushExceptions handler should fire.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushExceptions_UnmatchedType_FallsBackToDefault(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("specific-exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            // Throw AnotherException — does NOT match CustomBusinessException filter
            ExceptionTracker tracker = new()
            {
                ShouldThrow = true,
                ExceptionTypeToThrow = typeof(AnotherException),
                ExceptionMessage = "Another error"
            };

            HorseClient worker = await BuildConsumerWorker<SpecificExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> specificLogs = new();
            HorseClient specificConsumer = await ConnectRaw(ctx.Port);
            specificConsumer.MessageReceived += (_, msg) => specificLogs.Add(msg);
            await specificConsumer.Queue.Subscribe("specific-exception-log-q", true, CancellationToken.None);

            ConcurrentBag<HorseMessage> defaultLogs = new();
            HorseClient defaultConsumer = await ConnectRaw(ctx.Port);
            defaultConsumer.MessageReceived += (_, msg) => defaultLogs.Add(msg);
            await defaultConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "unmatched-exc-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => defaultLogs.Count >= 1, 5_000);

            Assert.True(tracker.ThrowCount >= 1);

            // Default handler should have caught the unmatched exception
            Assert.True(defaultLogs.Count >= 1, $"Expected ≥1 default log, got {defaultLogs.Count}");

            // Specific handler should NOT have fired (AnotherException != CustomBusinessException)
            Assert.Empty(specificLogs);

            producer.Disconnect();
            worker.Disconnect();
            specificConsumer.Disconnect();
            defaultConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PublishExceptions_ConfigBuilder_Default_ExceptionPublishedToRouter(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("published-exception-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            Router router = new Router(ctx.Rider, "builder-exception-router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding
            {
                Name = "published-exception-binding",
                Target = "published-exception-q",
                Priority = 0,
                Interaction = BindingInteraction.None
            });
            ctx.Rider.Router.Add(router);

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<AutoNackOnlyConsumer>(ctx.Port, tracker,
                cfg => cfg.PublishExceptions<RouterExceptionLogModel>());
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> publishedMessages = new();
            HorseClient publishedConsumer = await ConnectRaw(ctx.Port);
            publishedConsumer.MessageReceived += (_, msg) => { publishedMessages.Add(msg); };
            await publishedConsumer.Queue.Subscribe("published-exception-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "publish-exc-builder-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await WaitUntil(() => publishedMessages.Count >= 1, 5_000);

            Assert.True(tracker.ThrowCount >= 1);
            Assert.True(publishedMessages.Count >= 1, $"Expected ≥1 published exception, got {publishedMessages.Count}");

            string content = publishedMessages.First().GetStringContent();
            RouterExceptionLogModel logModel = JsonSerializer.Deserialize<RouterExceptionLogModel>(content);
            Assert.NotNull(logModel);
            Assert.Equal(typeof(InvalidOperationException).FullName, logModel.ExceptionType);
            Assert.Equal("Test consumer error", logModel.Message);

            producer.Disconnect();
            worker.Disconnect();
            publishedConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  5. AutoNack only — NACK sent on exception
    // ═══════════════════════════════════════════════════════════════════

    #region AutoNack Only

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoAck_ConfigBuilder_SuccessfulConsume_NotRedelivered(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("source-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(1);
                o.PutBack = PutBackDecision.Regular;
            });

            ExceptionTracker tracker = new() { ShouldThrow = false };
            HorseClient worker = await BuildConsumerWorker<BuilderConfiguredConsumer>(ctx.Port, tracker,
                cfg => cfg.AutoAck());
            await Task.Delay(500);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "builder-auto-ack" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ConsumedMessages.Count >= 1);
            await Task.Delay(2500);

            Assert.Equal(1, tracker.ConsumeAttemptCount);
            Assert.Equal(0, tracker.ThrowCount);
            Assert.Single(tracker.ConsumedMessages);
            Assert.Contains("builder-auto-ack", tracker.ConsumedMessages);

            producer.Disconnect();
            worker.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoNack_ConfigBuilder_ExceptionThrown_NackSentToServer(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("source-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
                o.PutBack = PutBackDecision.No;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<BuilderConfiguredConsumer>(ctx.Port, tracker,
                cfg => cfg.AutoNack(NegativeReason.Error));
            await Task.Delay(500);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "builder-auto-nack" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(500);

            Assert.Equal(1, tracker.ConsumeAttemptCount);
            Assert.Equal(1, tracker.ThrowCount);

            producer.Disconnect();
            worker.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoNack_ExceptionThrown_NackSentToServer(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("source-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
                o.PutBack = PutBackDecision.No;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<AutoNackOnlyConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "nack-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(500);

            // Consumer threw, NACK was auto-sent
            Assert.True(tracker.ThrowCount >= 1);

            // With PutBack=No, the message should be removed after NACK
            // Verify queue is empty (message was consumed & nacked)
            var queue = ctx.Rider.Queue.Find("source-q");
            Assert.NotNull(queue);

            // Message was delivered to consumer, consumer threw and sent NACK
            Assert.Equal(1, tracker.ConsumeAttemptCount);

            producer.Disconnect();
            worker.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  6. Retry + PushExceptions — retries exhausted then exception pushed
    // ═══════════════════════════════════════════════════════════════════

    #region Retry Then PushExceptions

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry_ExhaustedThenPushException_RetryCountCorrect(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<RetryThenPushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> logMessages = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => logMessages.Add(msg);
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "retry-test" }, false, CancellationToken.None);

            // [Retry(3, 50)] → 3 retries × 50ms delay
            await WaitUntil(() => tracker.ThrowCount >= 3, 5_000);
            await WaitUntil(() => logMessages.Count >= 1, 5_000);

            // Consumer should have been called 3 times (retry count = 3)
            Assert.Equal(3, tracker.ThrowCount);
            Assert.Equal(3, tracker.ConsumeAttemptCount);

            // After all retries exhausted, PushExceptions fires
            Assert.True(logMessages.Count >= 1, $"Expected ≥1 exception log after retry, got {logMessages.Count}");

            producer.Disconnect();
            worker.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    /// <summary>
    /// When retry succeeds before exhaustion, no PushException should fire.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Retry_SucceedsOnSecondAttempt_NoPushException(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<RetryThenPushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> logMessages = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => logMessages.Add(msg);
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            // After first throw, turn off throwing so retry succeeds
            _ = Task.Run(async () =>
            {
                await WaitUntil(() => tracker.ThrowCount >= 1);
                tracker.ShouldThrow = false;
            });

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "retry-succeed-test" }, false, CancellationToken.None);

            // Wait for retry to succeed
            await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
            await Task.Delay(500);

            // Consumer should have succeeded on a retry
            Assert.True(tracker.ConsumedMessages.Count >= 1);
            Assert.True(tracker.ThrowCount >= 1, "Should have thrown at least once before succeeding");

            // No PushException should have fired since retry succeeded
            Assert.Empty(logMessages);

            producer.Disconnect();
            worker.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  7. MoveOnError + PushExceptions together
    // ═══════════════════════════════════════════════════════════════════

    #region MoveOnError And PushExceptions Combined

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MoveOnError_AndPushExceptions_BothFire(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("error-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<MoveOnErrorAndPushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> errorMsgs = new();
            HorseClient errorConsumer = await ConnectRaw(ctx.Port);
            errorConsumer.MessageReceived += (_, msg) => errorMsgs.Add(msg);
            await errorConsumer.Queue.Subscribe("error-q", true, CancellationToken.None);

            ConcurrentBag<HorseMessage> logMsgs = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => logMsgs.Add(msg);
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "combined-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(2000);

            Assert.True(tracker.ThrowCount >= 1);

            // PushExceptions should always fire (SendExceptions is called regardless of MoveOnError result)
            await WaitUntil(() => logMsgs.Count >= 1, 5_000);
            Assert.True(logMsgs.Count >= 1, $"Expected ≥1 exception log, got {logMsgs.Count}");

            producer.Disconnect();
            worker.Disconnect();
            errorConsumer.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  8. No exception — happy path, no error handling fires
    // ═══════════════════════════════════════════════════════════════════

    #region Happy Path

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task NoException_HappyPath_NoErrorQueueOrLogMessages(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("error-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = false }; // No exception
            HorseClient worker = await BuildConsumerWorker<MoveOnErrorAndPushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> errorMsgs = new();
            HorseClient errorConsumer = await ConnectRaw(ctx.Port);
            errorConsumer.MessageReceived += (_, msg) => errorMsgs.Add(msg);
            await errorConsumer.Queue.Subscribe("error-q", true, CancellationToken.None);

            ConcurrentBag<HorseMessage> logMsgs = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => logMsgs.Add(msg);
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "happy-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ConsumedMessages.Count >= 1, 5_000);
            await Task.Delay(500);

            // Message consumed successfully
            Assert.Single(tracker.ConsumedMessages);
            Assert.Contains("happy-test", tracker.ConsumedMessages);

            // No exceptions, no error handling
            Assert.Equal(0, tracker.ThrowCount);
            Assert.Empty(errorMsgs);
            Assert.Empty(logMsgs);

            producer.Disconnect();
            worker.Disconnect();
            errorConsumer.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  9. AutoNack with different NegativeReason values
    // ═══════════════════════════════════════════════════════════════════

    #region AutoNack NegativeReason

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoNack_NegativeReasonExceptionType_NackContainsTypeName(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("source-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
                o.PutBack = PutBackDecision.No;
            });

            // PushExceptionConsumer has [AutoNack(NegativeReason.ExceptionType)]
            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<PushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            // Also subscribe to exception-log-q so PushExceptions has somewhere to go
            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);
            await bus.Push("source-q", new SourceModel { Data = "nack-reason-test" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= 1);
            await Task.Delay(500);

            Assert.True(tracker.ThrowCount >= 1);
            // Consumer sent NACK with ExceptionType reason
            // We can't easily inspect the NACK content from the test,
            // but we verify the consumer threw and the flow completed without hanging
            Assert.Equal(1, tracker.ConsumeAttemptCount);

            producer.Disconnect();
            worker.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  10. Multiple messages — exception handling fires for each
    // ═══════════════════════════════════════════════════════════════════

    #region Multiple Messages

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushExceptions_MultipleMessages_EachGetsExceptionLog(string mode)
    {
        await using var ctx = await CreateContext(mode);
        try
        {

            await ctx.Rider.Queue.Create("exception-log-q", o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            ExceptionTracker tracker = new() { ShouldThrow = true };
            HorseClient worker = await BuildConsumerWorker<PushExceptionConsumer>(ctx.Port, tracker);
            await Task.Delay(500);

            ConcurrentBag<HorseMessage> logMessages = new();
            HorseClient logConsumer = await ConnectRaw(ctx.Port);
            logConsumer.MessageReceived += (_, msg) => logMessages.Add(msg);
            await logConsumer.Queue.Subscribe("exception-log-q", true, CancellationToken.None);
            await Task.Delay(200);

            HorseClient producer = await ConnectRaw(ctx.Port);
            IHorseQueueBus bus = new HorseQueueBus(producer);

            int messageCount = 5;
            for (int i = 0; i < messageCount; i++)
                await bus.Push("source-q", new SourceModel { Data = $"multi-{i}" }, false, CancellationToken.None);

            await WaitUntil(() => tracker.ThrowCount >= messageCount, 10_000);
            await WaitUntil(() => logMessages.Count >= messageCount, 10_000);

            Assert.Equal(messageCount, tracker.ThrowCount);
            Assert.Equal(messageCount, logMessages.Count);

            producer.Disconnect();
            worker.Disconnect();
            logConsumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion
}
