# Producing Messages

Producers send messages into queues. Horse Messaging supports model-based publishing with attributes and explicit queue targeting.

## Using IHorseQueueBus

When using dependency injection, inject `IHorseQueueBus` to publish messages:

```csharp
public class OrderService
{
    private readonly IHorseQueueBus _bus;

    public OrderService(IHorseQueueBus bus)
    {
        _bus = bus;
    }

    public async Task CreateOrder(Order order)
    {
        // Process order...

        // Push to queue (queue name resolved from model type or [QueueName] attribute)
        HorseResult result = await _bus.Push(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CreatedAt = DateTime.UtcNow
        }, waitForCommit: true);

        if (result.Code != HorseResultCode.Ok)
            Console.WriteLine($"Push failed: {result.Code}");
    }
}
```

The `waitForCommit: true` parameter tells the client to wait for a commit response from the server before completing the `Push` call. When the commit arrives depends on the queue's [`CommitWhen`](queue-options.md#commitwhen) option. See [Commit Behavior](#commit-behavior) below for details.

## Using HorseClient Directly

Without DI, use the `HorseClient.Queue` operator:

```csharp
HorseClient client = new HorseClient();
client.Connect("horse://localhost:26222");

// Push a serialized model (queue name resolved from type or [QueueName] attribute)
await client.Queue.Push(new OrderCreatedEvent { OrderId = 42 }, waitForCommit: true);

// Push with a specific queue name
await client.Queue.Push("my-custom-queue", new OrderCreatedEvent { OrderId = 42 }, waitForCommit: true);
```

## Push to Named Queues

By default, the queue name is resolved from the model type name or the `[QueueName]` attribute. You can override this:

```csharp
// Explicit queue name
await bus.Push("high-priority-orders", new OrderEvent { ... }, waitForCommit: true);
```

## Push Overloads

The `QueueOperator.Push` method has several overloads:

```csharp
// Model-based (queue name from type/attribute)
Task<HorseResult> Push<T>(T model, bool waitForCommit, ...)

// Model-based with explicit queue name
Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, ...)

// Model-based with explicit message ID
Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, ...)

// Raw binary content
Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, ...)
Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, ...)
```

All overloads accept optional `messageHeaders` and `CancellationToken` parameters.

The `IHorseQueueBus` interface additionally supports a `partitionLabel` parameter for [partition-aware routing](partitioned-queues.md):

```csharp
await bus.Push(new OrderEvent { ... }, waitForCommit: true, partitionLabel: "tenant-42");
```

## Bulk Push

Push multiple messages in a single batch:

```csharp
var items = new List<OrderEvent> { ... };
client.Queue.PushBulk("orders", items, (message, committed) =>
{
    Console.WriteLine($"Message {message.MessageId}: committed={committed}");
});
```

## Model-Based Publishing with Attributes

Decorate your model class with attributes to define queue behavior. These attributes are read when the queue is auto-created on first push:

```csharp
[QueueName("orders")]
[QueueType(MessagingQueueType.RoundRobin)]
[QueueManager("Persistent")]
[AcknowledgeTimeout(30)]
[MessageTimeout(MessageTimeoutPolicy.Delete, 3600)]
[PutBack(PutBack.Regular, 500)]
[DelayBetweenMessages(50)]
public class OrderEvent
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; }
}
```

| Attribute | Description |
|-----------|-------------|
| `[QueueName]` | Explicit queue name. If omitted, the class name is used. |
| `[QueueType]` | Queue delivery type (Push, RoundRobin, Pull). |
| `[QueueManager]` | Targets a specific storage backend by name. |
| `[AcknowledgeTimeout]` | Acknowledge timeout in seconds. |
| `[MessageTimeout]` | Message expiration policy and duration in seconds. |
| `[PutBack]` | Put-back decision and delay in milliseconds. |
| `[DelayBetweenMessages]` | Delay between message deliveries in milliseconds. |
| `[HighPriorityMessage]` | Message is inserted at the front of the queue. |
| `[QueueTopic]` | Sets a topic string on the queue. Used for topic-based routing. See [Queue Topics](queue-topics.md). |
| `[UniqueIdCheck]` | Enables message ID uniqueness checking. |

## Push with Custom Headers

You can attach custom headers to messages:

```csharp
var headers = new List<KeyValuePair<string, string>>
{
    new("X-Tenant-Id", "tenant-42"),
    new("X-Priority", "high")
};

await client.Queue.Push(new OrderEvent { ... }, waitForCommit: true, messageHeaders: headers);
```

## Commit Behavior

The `waitForCommit` parameter controls whether the `Push` call blocks until the server confirms the message was processed.

### `waitForCommit: false`

The client sends the message and returns immediately without waiting for any server response. The `Push` call completes as soon as the bytes are written to the socket. You get no feedback about whether the message was stored, delivered, or acknowledged. This is fire-and-forget:

```csharp
// Fire-and-forget: returns immediately, no guarantee the server processed the message
await bus.Push(new MetricEvent { Value = 42 }, waitForCommit: false);
```

### `waitForCommit: true`

The client sends the message and waits for a commit response from the server. The `Push` call blocks (asynchronously) until the server sends back a response or the client's `ResponseTimeout` expires.

**When** the server sends the commit depends on the queue's [`CommitWhen`](queue-options.md#commitwhen) option:

| CommitWhen | Producer receives commit when... |
|------------|----------------------------------|
| `None` | No commit is ever sent. The `Push` call **times out** and returns a timeout result. Do not use `waitForCommit: true` with `CommitWhen.None`. |
| `AfterReceived` | The server has received and stored the message in the queue. This is the fastest commit — it confirms the message is safely queued but not yet delivered. |
| `AfterSent` | The message has been sent to at least one consumer. If there are no consumers, the `Push` call **times out** because the commit is never triggered. |
| `AfterAcknowledge` | A consumer has acknowledged the message. If the consumer never acks (or ack times out), the `Push` call returns a failure or timeout. |

**Return value:** The `HorseResult` returned by `Push` contains:
- `HorseResultCode.Ok` — Commit received, message was successfully processed.
- `HorseResultCode.Failed` — Server rejected the message or a consumer sent a negative ack.
- `HorseResultCode.RequestTimeout` — No response within the client's `ResponseTimeout`.
- `HorseResultCode.LimitExceeded` — Message size or queue message limit exceeded.
- `HorseResultCode.NoConsumers` — No consumers subscribed (for some queue configurations).

```csharp
// Wait for commit: blocks until the server confirms based on CommitWhen
HorseResult result = await bus.Push(new OrderEvent { OrderId = 1 }, waitForCommit: true);

if (result.Code == HorseResultCode.Ok)
    Console.WriteLine("Message committed successfully");
else
    Console.WriteLine($"Push failed: {result.Code} - {result.Reason}");
```
