# Queue Lifecycle

This document covers how queues are created, their runtime states, and how they are destroyed.

## Queue Creation

Queues can be created in three ways:

### 1. Auto Creation

When `AutoQueueCreation` is enabled (default: `true`), the server automatically creates a queue the first time a client pushes a message or subscribes to a queue that doesn't exist.

The new queue inherits the server's default `QueueOptions`. Attributes on the model class or consumer can override individual options.

```csharp
// Server-side: enable auto-creation (enabled by default)
cfg.Options.AutoQueueCreation = true;
```

### 2. Programmatic Creation (Server-Side)

Create queues explicitly in server code:

```csharp
// With default options
HorseQueue queue = await rider.Queue.Create("my-queue");

// With custom options
HorseQueue queue = await rider.Queue.Create("my-queue", o =>
{
    o.Type = QueueType.Push;
    o.Acknowledge = QueueAckDecision.None;
    o.AutoDestroy = QueueDestroy.Empty;
});
```

### 3. Client-Initiated Creation

When a client pushes or subscribes and the queue doesn't exist, the server creates it using:
1. Server default options (lowest priority)
2. Options from the client's subscription/push headers
3. Attributes on the model class (highest priority)

## Queue Status

Each queue has a `Status` property that controls its runtime behavior:

| Status | Push Allowed | Consume Allowed | Description |
|--------|:------------:|:---------------:|-------------|
| `NotInitialized` | ✗ | ✗ | Queue has been defined but not yet started. |
| `Running` | ✓ | ✓ | Normal operation. Messages flow in and out. |
| `OnlyPush` | ✓ | ✗ | Producers can push messages, but consumers do not receive them. Messages accumulate in the queue. |
| `OnlyConsume` | ✗ | ✓ | Consumers can receive existing messages, but new pushes are rejected. |
| `Paused` | ✗ | ✗ | All operations are suspended. Messages remain in the queue. |
| `Syncing` | ✗ | ✗ | Queue messages are being synchronized (cluster replication). |

### Changing Status at Runtime

```csharp
HorseQueue queue = rider.Queue.Find("my-queue");

// Pause the queue
queue.SetStatus(QueueStatus.Paused);

// Resume
queue.SetStatus(QueueStatus.Running);

// Accept pushes only (drain mode)
queue.SetStatus(QueueStatus.OnlyPush);
```

## Auto Destroy

The `AutoDestroy` option controls when a queue is automatically removed:

| Value | Condition |
|-------|-----------|
| `Disabled` | Queue is never auto-destroyed. |
| `NoMessages` | Destroyed when the queue becomes empty (0 messages), regardless of subscriber count. |
| `NoConsumers` | Destroyed when the last consumer unsubscribes, regardless of message count. |
| `Empty` | Destroyed when there are no messages **and** no consumers. |

```csharp
cfg.Options.AutoDestroy = QueueDestroy.Empty;
```

> **Note:** For partitioned queues, the parent queue's `AutoDestroy` and the partition's `AutoDestroy` are independent settings. See [Partitioned Queues](partitioned-queues.md).

## Destroying Queues Programmatically

```csharp
// Remove a queue by name
await rider.Queue.Remove("my-queue");

// Or via the queue instance
HorseQueue queue = rider.Queue.Find("my-queue");
if (queue != null)
    await rider.Queue.Remove(queue);
```

When a queue is destroyed:
- All remaining messages are discarded (or flushed to disk if persistent).
- All subscribed consumers are unsubscribed.
- The queue event handler's `OnRemoved` is called.

## Queue Configuration Persistence

By default, the server persists queue configurations to a JSON file. When the server restarts, it recreates queues from this file.

### Default Behavior

Queue configurations are saved to `data/queues.json` (relative to the working directory). This includes:
- Queue name, topic, and manager name
- All queue options (type, ack, timeouts, limits, etc.)
- Partition configuration (if any)

### Custom Configuration Path

```csharp
cfg.UseCustomPersistentConfigurator(
    new QueueOptionsConfigurator(rider, "custom/path/queues.json")
);
```

### Disabling Persistence

```csharp
cfg.UseCustomPersistentConfigurator(null);
```

When persistence is disabled, all queues are lost on restart and must be recreated (either programmatically or via auto-creation).

### What Is Persisted

The configuration file stores **queue definitions and options**, not the messages themselves. Message persistence is handled separately by the storage backend (see [Storage Backends](storage-backends.md)).

| Item | Saved in config | Saved by storage |
|------|:---------------:|:----------------:|
| Queue name & options | ✓ | ✗ |
| Partition config | ✓ | ✗ |
| Messages | ✗ | ✓ (persistent only) |
| Consumer subscriptions | ✗ | ✗ |

