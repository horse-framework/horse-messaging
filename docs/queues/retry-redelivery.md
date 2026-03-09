# Retry & Redelivery

Horse Messaging provides two complementary mechanisms for handling transient failures:

- **Retry** — Client-side, in-process retry of the `Consume` method when an exception is thrown.  
- **Redelivery** — Server-side tracking of how many times a message has been delivered to consumers across put-back cycles.

These are independent features that can be used separately or together.

---

## Retry (Client-Side)

The `[Retry]` attribute on a consumer class tells the client to re-invoke the `Consume` method if it throws an exception, **before** propagating the error to the outer exception handling pipeline ([AutoNack], [MoveOnError], [PushExceptions]).

### Basic Usage

```csharp
[Retry(3, 100)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // If this throws, the client retries up to 3 times with 100ms delay between attempts.
        await ProcessOrder(model);
    }
}
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Count` | `int` | `5` | Total number of attempts. `0` = maximum (capped at 100 to prevent infinite loops). |
| `DelayBetweenRetries` | `int` | `50` | Delay in milliseconds between each retry attempt. |
| `IgnoreExceptions` | `Type[]` | `null` | Exception types that should **not** be retried. If the thrown exception matches any type in this list, it propagates immediately without further retry. |

### How It Works

1. The consumer's `Consume` method is called.
2. If it throws an exception:
   - If the exception type is in `IgnoreExceptions`, the exception propagates immediately — **no retry**.
   - Otherwise, the client waits `DelayBetweenRetries` milliseconds.
   - The `Consume` method is called again.
3. If all attempts are exhausted, the exception propagates to the outer `catch` block where [AutoNack], [MoveOnError], and [PushExceptions] are evaluated.
4. If any attempt succeeds, processing stops and [AutoAck] sends an acknowledgment.

```
Attempt 1 → Exception → wait 100ms
Attempt 2 → Exception → wait 100ms
Attempt 3 → Exception → propagate to outer catch
                         → [AutoNack] sends NACK
                         → [MoveOnError] moves to error queue
                         → [PushExceptions] pushes exception model
```

### Ignore Exceptions

Some exceptions should not be retried because they represent permanent failures (e.g., validation errors, business rule violations):

```csharp
[Retry(5, 200, IgnoreExceptions = new[] { typeof(ValidationException), typeof(BusinessRuleException) })]
[AutoAck]
[AutoNack(NegativeReason.ExceptionType)]
public class PaymentConsumer : IQueueConsumer<PaymentModel>
{
    public async Task Consume(HorseMessage message, PaymentModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        if (model.Amount <= 0)
            throw new ValidationException("Invalid amount"); // NOT retried — propagates immediately

        await chargeService.Charge(model); // TimeoutException here → retried up to 5 times
    }
}
```

### Retry with Other Attributes

`[Retry]` is evaluated **inside** the consume pipeline. The outer exception handling only runs if retry is exhausted:

```csharp
[Retry(3, 50)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
[MoveOnError("payment-errors")]
[PushExceptions(typeof(PaymentExceptionLog))]
public class PaymentConsumer : IQueueConsumer<PaymentModel>
{
    // Attempt 1: exception → retry
    // Attempt 2: exception → retry
    // Attempt 3: exception → exhausted →
    //   1. [MoveOnError] clones message to "payment-errors" queue
    //   2. [AutoNack] sends NACK (if MoveOnError fails)
    //   3. [PushExceptions] pushes PaymentExceptionLog model
}
```

> **Important:** If retry succeeds on any attempt, none of the error handling attributes fire. [AutoAck] sends the acknowledgment as if the first attempt had succeeded.

---

## Redelivery (Server-Side)

Redelivery tracking is a **persistent queue** feature. When enabled, the server tracks how many times each message has been delivered to consumers. This count survives server restarts.

### Enabling Redelivery

Redelivery is enabled via the `useRedelivery` parameter when configuring persistent queues:

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(q =>
    {
        q.UsePersistentQueues(
            db => db.UseInstantFlush(),
            queue => { /* queue config */ },
            useRedelivery: true   // ← enables delivery count tracking
        );
    })
    .Build();
```

### How It Works

1. When a message is about to be sent to a consumer, the server increments its `DeliveryCount`.
2. The count is persisted to a `.delivery` file alongside the queue's `.hdb` database file.
3. If `DeliveryCount > 1` (i.e., this is a redelivery), the server adds a `Delivery` header to the message with the current count.
4. When the message is acknowledged (ACK) or removed, the redelivery entry is cleaned up.
5. On server restart, the `.delivery` file is loaded and counts are restored.

### Delivery Header

| Header | Value | When Added |
|---|---|---|
| `Delivery` | `"2"`, `"3"`, etc. | Only when `DeliveryCount > 1` (not on the first delivery) |

On the first delivery, no `Delivery` header is present. On the second delivery, the header value is `"2"`, and so on.

### Reading the Delivery Count

In your consumer, read the `Delivery` header to detect redeliveries:

```csharp
[AutoAck]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        string deliveryHeader = message.FindHeader(HorseHeaders.DELIVERY);
        int deliveryCount = string.IsNullOrEmpty(deliveryHeader) ? 1 : int.Parse(deliveryHeader);

        if (deliveryCount > 3)
        {
            // This message has been delivered 3+ times — likely a poison message.
            // Log and skip, or move to a dead-letter queue manually.
            await LogPoisonMessage(model, deliveryCount);
            return;
        }

        await ProcessOrder(model);
    }
}
```

### Redelivery Lifecycle

```
Producer sends message → Server stores message + DeliveryCount=0

First delivery:
  DeliveryCount → 1 (no Delivery header)
  Consumer NACKs → PutBack → message returns to queue

Second delivery:
  DeliveryCount → 2 (Delivery: 2 header added)
  Consumer NACKs → PutBack → message returns to queue

Third delivery:
  DeliveryCount → 3 (Delivery: 3 header added)
  Consumer ACKs → message removed, .delivery entry cleaned up
```

### Storage

Redelivery data is stored in a tab-separated text file:

```
msg-id-1	3
msg-id-2	1
msg-id-5	7
```

Each line contains the message ID and its delivery count. The file is rewritten on every update and reloaded on server startup.

### Without Redelivery

When `useRedelivery` is `false` (the default), the server does **not** track delivery counts. No `Delivery` header is added, and no `.delivery` file is created. `DeliveryCount` on the `QueueMessage` object remains `0`.

---

## Retry vs. Redelivery

| Aspect | Retry | Redelivery |
|---|---|---|
| **Where** | Client-side (in-process) | Server-side (across deliveries) |
| **Scope** | Single consumer, single delivery | Across multiple delivery attempts |
| **Configuration** | `[Retry]` attribute on consumer | `useRedelivery: true` in server config |
| **Survives restart** | No (in-memory only) | Yes (persisted to `.delivery` file) |
| **Header** | None | `Delivery` header with count |
| **Storage** | Memory | Persistent (`.hdb` + `.delivery` files) |
| **Use case** | Transient in-process errors (network timeout, deadlock) | Message-level tracking across NACK/put-back cycles |

### Using Both Together

You can combine Retry and Redelivery for defense-in-depth:

```csharp
// Server: persistent queues with redelivery tracking
q.UsePersistentQueues(db => db.UseInstantFlush(), useRedelivery: true);

// Queue: PutBack enabled so NACKed messages are redelivered
await rider.Queue.Create("orders", o =>
{
    o.PutBack = PutBackDecision.Regular;
    o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
});
```

```csharp
// Consumer: retry 3 times in-process, then NACK → server redelivers
[Retry(3, 100)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class OrderConsumer : IQueueConsumer<OrderModel>
{
    public async Task Consume(HorseMessage message, OrderModel model, HorseClient client,
        CancellationToken cancellationToken)
    {
        string deliveryHeader = message.FindHeader(HorseHeaders.DELIVERY);
        int serverDeliveryCount = string.IsNullOrEmpty(deliveryHeader) ? 1 : int.Parse(deliveryHeader);

        if (serverDeliveryCount > 5)
        {
            // Poison message — give up after 5 server-level redeliveries
            await MoveToDeadLetter(model);
            return;
        }

        // This may throw and trigger client-side retry (up to 3 attempts)
        await ProcessOrder(model);
    }
}
```

In this setup:
1. The consumer retries up to 3 times in-process (client-side).
2. If all 3 retries fail, [AutoNack] sends a NACK.
3. The server puts the message back in the queue (PutBack=Regular).
4. The server increments the delivery count and redelivers.
5. The consumer checks the `Delivery` header to detect poison messages after 5 server-level redeliveries.

See also: [Acknowledgment & Reliability](acknowledgment.md) | [Dead-Letter Queues & Exception Handling](dead-letter-exceptions.md) | [Queue Options](queue-options.md)
