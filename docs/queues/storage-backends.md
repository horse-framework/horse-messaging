# Storage Backends

Horse Messaging supports two built-in storage backends for queue messages: **Memory** and **Persistent**. You can also implement a custom queue manager.

## Memory Queues

Messages are stored in memory only. When the server restarts, all messages are lost. This is the simplest and fastest option.

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.UseMemoryQueues();
    })
    .Build();
```

### Named Memory Managers

You can register multiple memory managers with different names:

```csharp
cfg.UseMemoryQueues("Fast", queue =>
{
    queue.Options.Acknowledge = QueueAckDecision.None;
});

cfg.UseMemoryQueues("Reliable", queue =>
{
    queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
});
```

## Selecting a Queue Manager

When multiple queue managers are registered, you can specify which one a queue should use. There are three ways to do this:

### 1. Model Attribute

Use the `[QueueManager]` attribute on your message model class. When the queue is auto-created by the first push of this model, the server uses the specified manager:

```csharp
[QueueManager("Persistent")]
[QueueName("orders")]
public class OrderEvent { }

[QueueManager("Memory")]
[QueueName("metrics")]
public class MetricEvent { }
```

### 2. Explicit Queue Creation

Pass the `queueManagerName` parameter when creating a queue programmatically via `QueueOperator.Create`. Note that this callback uses the client-side `Horse.Messaging.Client.Queues.QueueOptions` shape: `Type` is `MessagingQueueType`, while string-backed options such as `Acknowledge` use protocol values like `"wait-for-ack"`:

```csharp
await client.Queue.Create("orders", options =>
{
    options.Type = MessagingQueueType.RoundRobin;
    options.Acknowledge = "wait-for-ack";
    options.MessageLimit = 1000;
}, queueManagerName: "Persistent");
```

### 3. Message Header

Add the `Queue-Manager` header to any push message via `messageHeaders`. This is useful when the queue does not exist yet and you want to control which manager is used at auto-creation time:

```csharp
var headers = new List<KeyValuePair<string, string>>
{
    new(HorseHeaders.QUEUE_MANAGER, "Persistent")
};

await client.Queue.Push("orders", content, true, headers, null, cancellationToken);
```

> **Note:** The queue manager is determined only at queue creation time. Once a queue is created with a specific manager, subsequent messages do not change it — the header is stripped from the message before delivery.

## Persistent Queues

Messages are written to disk. When the server restarts, messages are reloaded from the database files. Requires the `Horse.Messaging.Data` NuGet package.

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.UsePersistentQueues();
    })
    .Build();
```

### Data Configuration

Use the `DataConfigurationBuilder` to customize storage behavior:

```csharp
cfg.UsePersistentQueues(dataConfig =>
{
    // Auto flush writes to disk at regular intervals
    dataConfig.UseAutoFlush(TimeSpan.FromSeconds(1));

    // Or flush every message immediately (slower but safer)
    // dataConfig.UseInstantFlush();

    // Enable auto-shrink to compact database files periodically
    dataConfig.SetAutoShrink(true, TimeSpan.FromMinutes(5));

    // Keep a backup of the previous database file after shrink
    dataConfig.KeepLastBackup(true);

    // Use separate folders for each queue's data
    dataConfig.UseSeparateFolder(true);
});
```

### Flush Strategies

| Strategy | Method | Behavior |
|----------|--------|----------|
| Auto Flush | `UseAutoFlush(interval)` | Buffers writes and flushes to disk at the specified interval. Good balance of performance and durability. |
| Instant Flush | `UseInstantFlush()` | Every message write is immediately flushed to disk. Maximum durability, lower throughput. |

### Auto-Shrink

Over time, database files grow as messages are added and removed. Auto-shrink compacts the file by removing deleted entries:

```csharp
dataConfig.SetAutoShrink(true, TimeSpan.FromMinutes(10));
```

### With Queue Configuration Callback

```csharp
cfg.UsePersistentQueues(
    dataConfig =>
    {
        dataConfig.UseAutoFlush(TimeSpan.FromSeconds(2));
    },
    queue =>
    {
        queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        queue.Options.CommitWhen = CommitWhen.AfterReceived;
    }
);
```

## Redelivery Tracking

When using persistent queues, you can enable redelivery tracking. Each time a message is delivered to a consumer, its delivery count is incremented and persisted. On the second delivery and beyond, a `Delivery` header is added to the message with the current count:

```csharp
cfg.UsePersistentQueues(useRedelivery: true);
```

Consumers can read the `Delivery` header to detect redeliveries and implement poison message detection. For full details, see [Retry & Redelivery](retry-redelivery.md).

## Custom Queue Managers

For advanced scenarios, you can implement your own `IHorseQueueManager` and register it:

```csharp
cfg.UseCustomQueueManager("MyCustomManager", async builder =>
{
    var manager = new MyCustomQueueManager(builder.Queue);
    builder.Queue.Manager = manager;
    return manager;
});
```

## Using Both Memory and Persistent

You can register both backends simultaneously. The first registered manager becomes the default:

```csharp
cfg.UseMemoryQueues("Memory");
cfg.UsePersistentQueues("Persistent", dataConfig =>
{
    dataConfig.UseAutoFlush(TimeSpan.FromSeconds(1));
});
```

See [Selecting a Queue Manager](#selecting-a-queue-manager) above for how to target a specific backend per queue.
