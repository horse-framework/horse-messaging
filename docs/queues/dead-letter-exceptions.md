# Dead-Letter Queues & Exception Handling

When a consumer throws an exception during message processing, Horse Messaging provides several mechanisms to handle the failure gracefully. These mechanisms can be combined to build robust dead-letter queue (DLQ) patterns.

---

## Overview

The exception handling pipeline in `QueueConsumerExecutor` runs in this order when a consumer throws:

```
Consumer.Consume() throws Exception
        │
        ▼
   ┌─ [Retry] ─────────────────────────────┐
   │  Re-invoke Consume up to N times.      │
   │  If any attempt succeeds → [AutoAck]   │
   │  If all fail → exception propagates ───┘
        │
        ▼
   ┌─ [MoveOnError] ───────────────────────┐
   │  Clone message → set error queue target│
   │  Attach ExceptionDescription as JSON   │
   │  Send clone to error queue             │
   │  If success → ACK original message     │
   │  If failure → [AutoNack] original      │
   └────────────────────────────────────────┘
        │
        ▼
   ┌─ [AutoNack] ──────────────────────────┐
   │  Send NACK with configured reason      │
   │  (only if MoveOnError not configured   │
   │   or MoveOnError send failed)          │
   └────────────────────────────────────────┘
        │
        ▼
   ┌─ [PushExceptions] / [PublishExceptions]┐
   │  Create ITransportableException model  │
   │  Push to queue / Publish to router     │
   │  (always runs, regardless of above)    │
   └────────────────────────────────────────┘
```

---

## MoveOnError (Dead-Letter Queue)

`[MoveOnError]` is the primary mechanism for implementing dead-letter queues. When a consumer throws an exception, the **original message is cloned** and sent to the specified error queue with exception metadata attached.

### Usage

```csharp
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("order-errors")]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // If this throws, the original message is cloned and moved to "order-errors" queue
        await ProcessOrder(model);
    }
}
```

### How It Works

1. The original `HorseMessage` is cloned (including content and headers) with a new unique message ID.
2. An `ExceptionDescription` JSON is attached to the clone's `AdditionalContent`:
   ```json
   {
     "ExceptionType": "System.InvalidOperationException",
     "Message": "Order validation failed",
     "StackTrace": "at OrderConsumer.Consume() ...",
     "MachineName": "worker-01"
   }
   ```
3. The clone's target is set to the error queue name.
4. The clone is sent to the server via `SendAsync` with `waitForAcknowledge: true`.
5. **If the send succeeds:** An ACK is sent for the original message (removing it from the source queue).
6. **If the send fails:** A NACK is sent for the original message (if `[AutoNack]` is configured), so the server can put it back for retry.

### Error Queue Setup

The error queue must exist on the server (or auto-creation must be enabled):

```csharp
// Server-side: create the error queue explicitly
await rider.Queue.Create("order-errors", o =>
{
    o.Type = QueueType.Push;
    o.CommitWhen = CommitWhen.AfterReceived;
    o.Acknowledge = QueueAckDecision.None;
});
```

Or rely on `AutoQueueCreation = true` in the server's default queue options.

### Processing Dead-Letter Messages

Subscribe a consumer to the error queue to process or log failed messages:

```csharp
[QueueName("order-errors")]
[AutoAck]
public class OrderErrorConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // Read the exception metadata from AdditionalContent
        string additionalContent = message.GetStringAdditionalContent();
        ExceptionDescription error = JsonSerializer.Deserialize<ExceptionDescription>(additionalContent);
        
        await LogFailedOrder(model, error);
    }
}
```

---

## PushExceptions (Exception Logging to Queues)

`[PushExceptions]` pushes a **new model** (not the original message) to a queue when an exception occurs. The model must implement `ITransportableException` to receive exception context.

### Basic Usage (Catch-All)

```csharp
// 1. Define the exception log model
[QueueName("exception-logs")]
public class ExceptionLogEntry : ITransportableException
{
    public string ExceptionType { get; set; }
    public string Message { get; set; }
    public string SourceQueue { get; set; }

    public void Initialize(ExceptionContext context)
    {
        ExceptionType = context.Exception.GetType().FullName;
        Message = context.Exception.Message;
        SourceQueue = context.ConsumingMessage?.Target;
    }
}

// 2. Apply to consumer — catches ALL exceptions
[AutoAck]
[AutoNack(NegativeReason.Error)]
[PushExceptions(typeof(ExceptionLogEntry))]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        await ProcessOrder(model);
    }
}
```

When `OrderConsumer` throws any exception, an `ExceptionLogEntry` is created, `Initialize` is called with the exception context, and the model is pushed to the `"exception-logs"` queue.

### Type-Specific Exception Routing

You can route different exception types to different models (and therefore different queues):

```csharp
[QueueName("payment-errors")]
public class PaymentErrorLog : ITransportableException
{
    public string Detail { get; set; }
    
    public void Initialize(ExceptionContext context)
    {
        Detail = $"Payment failed: {context.Exception.Message}";
    }
}

[QueueName("generic-errors")]
public class GenericErrorLog : ITransportableException
{
    public string Detail { get; set; }
    
    public void Initialize(ExceptionContext context)
    {
        Detail = $"{context.Exception.GetType().Name}: {context.Exception.Message}";
    }
}

// PaymentException → PaymentErrorLog → "payment-errors" queue
// All other exceptions → GenericErrorLog → "generic-errors" queue
[PushExceptions(typeof(PaymentErrorLog), typeof(PaymentException))]
[PushExceptions(typeof(GenericErrorLog))]
[AutoAck]
[AutoNack(NegativeReason.ExceptionType)]
public class PaymentConsumer : IQueueConsumer<PaymentModel>
{
    public async Task Consume(HorseMessage message, PaymentModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        await chargeService.Charge(model);
    }
}
```

### Resolution Logic

When an exception occurs, the matching logic is:

1. Iterate through all `[PushExceptions(modelType, exceptionType)]` entries.
2. If the thrown exception is assignable to `exceptionType`, push that model. Mark as "found".
3. If **no** specific match is found, fall back to the default `[PushExceptions(modelType)]` (the overload without an exception type).

This means:
- **Specific matches** take priority. If `PaymentException` matches `typeof(PaymentException)`, only `PaymentErrorLog` is pushed.
- **Default catch-all** only fires when no specific match is found.
- Multiple specific matches can fire for the same exception (if the exception is assignable to multiple types).

### ITransportableException Interface

```csharp
public interface ITransportableException
{
    void Initialize(ExceptionContext context);
}
```

The `ExceptionContext` provides:

| Property | Type | Description |
|---|---|---|
| `Exception` | `Exception` | The thrown exception |
| `ConsumingMessage` | `HorseMessage` | The message that was being consumed when the error occurred |
| `Consumer` | `object` | The consumer executor instance |

---

## PublishExceptions (Exception Routing to Routers)

`[PublishExceptions]` works identically to `[PushExceptions]`, but instead of pushing to a queue, it **publishes to a router**. The model's routing is determined by its `[RouterName]` / `[ContentType]` attributes.

```csharp
[PublishExceptions(typeof(ErrorEvent))]
[PublishExceptions(typeof(CriticalErrorEvent), typeof(CriticalException))]
[AutoAck]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    // ...
}
```

Both `[PushExceptions]` and `[PublishExceptions]` can be used on the same consumer — they are evaluated independently.

---

## AutoNack (Automatic Negative Acknowledgment)

`[AutoNack]` sends a NACK to the server when the consumer throws an exception. The NACK tells the server the message was not successfully processed.

### Usage

```csharp
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    // On success: AutoAck sends ACK
    // On exception: AutoNack sends NACK with reason "Error"
}
```

### NegativeReason Options

| Value | NACK Reason Sent |
|---|---|
| `NegativeReason.None` | `"none"` |
| `NegativeReason.Error` | `"error"` |
| `NegativeReason.ExceptionType` | The exception class name (e.g., `"InvalidOperationException"`) |
| `NegativeReason.ExceptionMessage` | The exception message text |

### Interaction with MoveOnError

When both `[MoveOnError]` and `[AutoNack]` are present:

- If `[MoveOnError]` successfully sends the clone to the error queue → **ACK** is sent (not NACK).
- If `[MoveOnError]` fails to send the clone → **NACK** is sent via `[AutoNack]`.
- If `[MoveOnError]` is not present → **NACK** is sent via `[AutoNack]`.

---

## Combining Everything

Here is a fully configured consumer with all error handling mechanisms:

```csharp
[Retry(3, 100)]                                              // 1. Retry up to 3 times
[AutoAck]                                                     // 2. ACK on success
[AutoNack(NegativeReason.ExceptionType)]                      // 3. NACK on failure
[MoveOnError("order-dead-letters")]                           // 4. Move to DLQ
[PushExceptions(typeof(OrderExceptionLog))]                   // 5. Push exception log (catch-all)
[PushExceptions(typeof(PaymentFailureLog), typeof(PaymentException))] // 6. Push specific log
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        await ProcessOrder(model);
    }
}
```

**Execution flow when `ProcessOrder` throws a `PaymentException`:**

1. **[Retry]** retries `Consume` up to 3 times with 100ms delay.
2. All 3 attempts throw `PaymentException` → exception propagates to outer catch.
3. **[MoveOnError]** clones the message, attaches `ExceptionDescription` JSON, sends to `"order-dead-letters"`.
   - If send succeeds → ACK is sent for original message.
   - If send fails → **[AutoNack]** sends NACK with reason `"PaymentException"`.
4. **[PushExceptions]** evaluates:
   - `PaymentException` matches `typeof(PaymentException)` → `PaymentFailureLog` is pushed.
   - Since a specific match was found, the default `OrderExceptionLog` is **not** pushed.

**Execution flow when `ProcessOrder` throws an `IOException`:**

1. **[Retry]** retries 3 times → all fail.
2. **[MoveOnError]** clones and sends to `"order-dead-letters"`.
3. **[PushExceptions]** evaluates:
   - `IOException` does not match `typeof(PaymentException)`.
   - No specific match → default `OrderExceptionLog` is pushed.

---

## ExceptionDescription

When `[MoveOnError]` clones a message to the error queue, it attaches an `ExceptionDescription` JSON as the message's additional content:

```csharp
public class ExceptionDescription
{
    public string ExceptionType { get; set; }  // e.g., "System.InvalidOperationException"
    public string Message { get; set; }        // e.g., "Order validation failed"
    public string StackTrace { get; set; }     // Full stack trace
    public string MachineName { get; set; }    // e.g., "worker-01"
}
```

Read it from the error queue consumer:

```csharp
string json = message.GetStringAdditionalContent();
ExceptionDescription desc = JsonSerializer.Deserialize<ExceptionDescription>(json);
```

---

## Recommended Patterns

### Simple Dead-Letter Queue

```csharp
// Consumer: move failed messages to DLQ
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("orders-dlq")]
public class OrderConsumer : IQueueConsumer<OrderModel> { /* ... */ }
```

### Retry + Dead-Letter Queue

```csharp
// Consumer: retry 3 times, then move to DLQ
[Retry(3, 200)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("orders-dlq")]
public class OrderConsumer : IQueueConsumer<OrderModel> { /* ... */ }
```

### Centralized Exception Logging

```csharp
// Exception log model (shared across all consumers)
[QueueName("exception-logs")]
public class AppExceptionLog : ITransportableException
{
    public string Source { get; set; }
    public string ExceptionType { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }

    public void Initialize(ExceptionContext context)
    {
        Source = context.ConsumingMessage?.Target;
        ExceptionType = context.Exception.GetType().FullName;
        Message = context.Exception.Message;
        Timestamp = DateTime.UtcNow;
    }
}

// Apply to all consumers
[PushExceptions(typeof(AppExceptionLog))]
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderModel> { /* ... */ }

[PushExceptions(typeof(AppExceptionLog))]
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class PaymentConsumer : IQueueConsumer<PaymentModel> { /* ... */ }
```

### Poison Message Detection with Redelivery

Combine server-side redelivery tracking with a delivery count threshold:

```csharp
// Server: enable redelivery tracking
q.UsePersistentQueues(db => db.UseInstantFlush(), useRedelivery: true);

// Queue: put back on NACK
await rider.Queue.Create("orders", o =>
{
    o.PutBack = PutBackDecision.Regular;
    o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
});
```

```csharp
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("orders-poison")]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        string deliveryHeader = message.FindHeader(HorseHeaders.DELIVERY);
        int deliveryCount = string.IsNullOrEmpty(deliveryHeader) ? 1 : int.Parse(deliveryHeader);

        if (deliveryCount > 5)
            throw new PoisonMessageException("Message redelivered too many times");

        await ProcessOrder(model);
    }
}
```

See also: [Retry & Redelivery](retry-redelivery.md) | [Acknowledgment & Reliability](acknowledgment.md) | [Queue Options](queue-options.md)
