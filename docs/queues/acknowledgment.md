# Acknowledgment & Reliability

Horse Messaging provides a multi-layered acknowledgment system to ensure messages are processed reliably. This covers both the **producer side** (commit) and the **consumer side** (acknowledge).

## Consumer Acknowledgment Flow

```
Server ──deliver──▶ Consumer
                        │
                  ┌─────┴──────┐
                  │  Process   │
                  └─────┬──────┘
                        │
              ┌─────────┼─────────┐
              ▼         ▼         ▼
           Ack(+)    Ack(-)    Timeout
              │         │         │
           Remove    PutBack   PutBack
```

### Acknowledge Decisions

The queue's `Acknowledge` option controls the server's expectations. See also [Queue Options — Acknowledge](queue-options.md#acknowledge).

| Decision | Server behavior |
|----------|-----------------|
| `None` | Message is removed immediately after delivery. No ack expected. |
| `JustRequest` | Server asks for an ack but continues sending the next message without waiting. Unacked messages are handled by the put-back policy after timeout. |
| `WaitForAcknowledge` | Server waits for acknowledgment before delivering the next message. Behavior varies by queue type — see below. |

#### WaitForAcknowledge — Behavior by Queue Type

**Push (fan-out):**
The message is sent to **all** consumers simultaneously. The queue waits until **any one** consumer acknowledges (or the timeout expires) before dispatching the next message. It does **not** wait for all consumers to ack. The remaining consumers still have their own ack deadlines — if they don't ack in time, the put-back decision applies for their delivery.

**RoundRobin:**
The message is delivered to **one** consumer at a time. If a consumer is busy (hasn't acked its current message), the server skips it and tries the next available consumer. This means **FIFO ordering is not guaranteed** when multiple consumers are subscribed — different consumers may process messages concurrently and out of order. Strict FIFO is only guaranteed with **exactly one consumer**.

**Pull:**
This option has no effect. Pull queues are consumer-driven — the consumer fetches messages on demand and controls its own processing pace.

### Positive Acknowledge

A positive ack tells the server the message was processed successfully. The message is removed from the queue.

**Manual:**
```csharp
public async Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
    CancellationToken cancellationToken)
{
    // process...
    await client.SendAck(message);
}
```

**Automatic with `[AutoAck]`:**
```csharp
[AutoAck]
public class OrderConsumer : IQueueConsumer<OrderEvent>
{
    public Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // Ack is sent automatically after this method returns without exception
        return Task.CompletedTask;
    }
}
```

### Negative Acknowledge

A negative ack tells the server the message was not processed successfully. The server applies the put-back decision.

**Manual:**
```csharp
public async Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
    CancellationToken cancellationToken)
{
    try
    {
        // process...
    }
    catch
    {
        await client.SendNegativeAck(message, "Error");
        throw;
    }
}
```

**Automatic with `[AutoNack]`:**
```csharp
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderEvent>
{
    public Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // If this throws, a negative ack is sent automatically
        return ProcessOrder(model);
    }
}
```

### Acknowledge Timeout

If no ack (positive or negative) arrives within `AcknowledgeTimeout`, the server treats it as a failure and applies the put-back decision. See also [Queue Options — AcknowledgeTimeout](queue-options.md#acknowledgetimeout).

```csharp
cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
```

Or via attribute:

```csharp
[AcknowledgeTimeout(30)]
public class OrderConsumer : IQueueConsumer<OrderEvent> { ... }
```

## Put Back Behavior

When a message is negatively acknowledged or the ack timeout expires, the `PutBack` option determines what happens. See also [Queue Options — PutBack](queue-options.md#putback--putbackdelay).

| Decision | Effect |
|----------|--------|
| `No` | Message is permanently discarded. |
| `Regular` | Message goes back to the **end** of the queue. |
| `Priority` | Message goes back to the **front** of the queue (re-delivered first). |

`PutBackDelay` adds a cooling-off period (in milliseconds) before the message is re-queued, preventing tight retry loops:

```csharp
cfg.Options.PutBack = PutBackDecision.Regular;
cfg.Options.PutBackDelay = 2000; // 2 seconds before put-back
```

## Producer Commit

The commit mechanism lets producers know the fate of their message on the server side. This is configured with `CommitWhen`. See also [Queue Options — CommitWhen](queue-options.md#commitwhen) and [Producing Messages — Commit Behavior](producing-messages.md#commit-behavior).

| CommitWhen | Producer receives response when... |
|------------|-------------------------------------|
| `None` | Never. No commit is sent. If `waitForCommit: true` is used, the call **times out**. |
| `AfterReceived` | Server received and stored the message. Fastest commit — confirms the message is safely queued. |
| `AfterSent` | Message was delivered to at least one consumer. If there are no consumers, the call **times out**. |
| `AfterAcknowledge` | A consumer acknowledged the message. If the consumer never acks, the call returns a failure or times out. |

### waitForCommit Parameter

On the client side, the `waitForCommit` parameter controls whether the `Push` call blocks:

- **`waitForCommit: true`** — The client waits for the server to send a commit response. The `HorseResult` contains the actual result code (`Ok`, `Failed`, `RequestTimeout`, `LimitExceeded`, etc.).
- **`waitForCommit: false`** — Fire-and-forget. The client sends the message and returns immediately. No feedback about whether the server processed the message.

```csharp
// Wait for commit — blocks until server confirms
HorseResult result = await bus.Push(order, true, cancellationToken);
if (result.Code == HorseResultCode.Ok)
    Console.WriteLine("Message committed");

// Fire-and-forget — returns immediately
await bus.Push(metrics, false, cancellationToken);
```

## Redelivery Headers

When persistent queues have redelivery tracking enabled, each message carries a header indicating how many times it has been delivered:

```csharp
cfg.UsePersistentQueues(useRedelivery: true);
```

In the consumer:

```csharp
public Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
    CancellationToken cancellationToken)
{
    string countHeader = message.FindHeader("Redelivery-Count");
    int redeliveryCount = string.IsNullOrEmpty(countHeader) ? 0 : int.Parse(countHeader);

    if (redeliveryCount > 3)
    {
        // Move to dead-letter queue or log
    }

    return Task.CompletedTask;
}
```

Or use the `[MoveOnError]` attribute to automate dead-letter routing:

```csharp
[MoveOnError("orders-dead-letter")]
[Retry(3, delayBetweenRetries: 500)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderEvent>
{
    public Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        return ProcessOrder(model);
    }
}
```

## Retry

The `[Retry]` attribute re-invokes the `Consume` method if it throws an exception:

```csharp
[Retry(5, delayBetweenRetries: 200)]
public class OrderConsumer : IQueueConsumer<OrderEvent> { ... }
```

| Parameter | Description |
|-----------|-------------|
| `count` | Maximum retry attempts (max 100). |
| `delayBetweenRetries` | Delay in milliseconds between retries. Default is 50ms. |
| `ignoreExceptions` | Exception types that should **not** trigger a retry. |

If all retries are exhausted and `[MoveOnError]` is set, the message is moved to the error queue.

For full details on retry mechanics, ignored exceptions, and interaction with redelivery tracking, see [Retry & Redelivery](retry-redelivery.md). For dead-letter queue patterns and `[PushExceptions]`, see [Dead-Letter Queues & Exception Handling](dead-letter-exceptions.md).

## Reliability Patterns Summary

| Pattern | Configuration |
|---------|---------------|
| **At-most-once** | `Acknowledge = None` |
| **At-least-once** | `Acknowledge = WaitForAcknowledge` + `PutBack = Regular` |
| **Strict FIFO** | `Acknowledge = WaitForAcknowledge` + `Type = RoundRobin` + **single consumer** |
| **Dead-letter queue** | `[MoveOnError("dlq")]` + `[Retry(3)]` |
| **Producer confirmation** | `CommitWhen = AfterReceived` + `Push(model, waitForCommit: true)` |

> **Note:** With `RoundRobin` + `WaitForAcknowledge` + multiple consumers, messages are distributed across consumers but strict FIFO ordering is **not** guaranteed. Use a single consumer if ordering matters.

