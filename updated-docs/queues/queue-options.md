# Queue Options

Every queue has its own `QueueOptions` instance. When a queue is created, the server's default options are applied first, then any overrides specified during creation take effect.

## Options Reference

### Acknowledge

| Property | `Acknowledge` |
|----------|---------------|
| Type | `QueueAckDecision` enum |
| Default | `WaitForAcknowledge` |

Controls whether the server expects an acknowledgment from the consumer after delivering a message. For detailed behavior, put-back policies, and reliability patterns, see [Acknowledgment & Reliability](acknowledgment.md).

| Value | Behavior |
|-------|----------|
| `None` | No acknowledgment requested. Message is removed immediately after delivery. |
| `JustRequest` | Acknowledgment is requested, but the queue keeps sending the next messages without waiting. If no ack arrives within the timeout, the put-back decision applies. |
| `WaitForAcknowledge` | The queue waits for a consumer to acknowledge before sending the next message. **Push:** The message is sent to all consumers simultaneously (fan-out). The queue waits until **any one** consumer acknowledges (or the timeout expires) before dispatching the next message. It does not wait for all consumers to ack. **RoundRobin:** The message is delivered to one consumer at a time. Busy consumers (haven't acked) are skipped. FIFO ordering is **not guaranteed** with multiple consumers — it is only guaranteed with exactly one consumer. **Pull:** This option has no effect; pull queues are consumer-driven. |

```csharp
cfg.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
```

---

### CommitWhen

| Property | `CommitWhen` |
|----------|--------------|
| Type | `CommitWhen` enum |
| Default | `AfterReceived` |

Determines when the server sends a commit (confirmation) back to the producer. This option works in conjunction with the `waitForCommit` parameter on the client side. See [Commit Behavior](producing-messages.md#commit-behavior) for how `waitForCommit: true` and `waitForCommit: false` interact with this option.

| Value | Behavior |
|-------|----------|
| `None` | No commit is sent to the producer. |
| `AfterReceived` | Commit is sent as soon as the server receives and stores the message. |
| `AfterSent` | Commit is sent after the message has been sent to at least one consumer. |
| `AfterAcknowledge` | Commit is sent after a consumer acknowledges the message. |

```csharp
cfg.Options.CommitWhen = CommitWhen.AfterReceived;
```

---

### AcknowledgeTimeout

| Property | `AcknowledgeTimeout` |
|----------|----------------------|
| Type | `TimeSpan` |
| Default | `15 seconds` |

Maximum time the server waits for an acknowledge message from a consumer. If the timeout expires, the put-back decision is applied to the message.

```csharp
cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
```

---

### MessageTimeout

| Property | `MessageTimeout` |
|----------|------------------|
| Type | `MessageTimeoutStrategy` |
| Default | No timeout |

Controls what happens when a message stays in the queue longer than the specified duration.

**MessageTimeoutStrategy properties:**

| Property | Type | Description |
|----------|------|-------------|
| `MessageDuration` | `int` | Timeout duration in seconds. `0` = no timeout. |
| `Policy` | `MessageTimeoutPolicy` | What to do when the message expires. |
| `TargetName` | `string` | Target queue or router name (for `PushQueue` / `PublishRouter` policies). |

**MessageTimeoutPolicy values:**

| Value | Behavior |
|-------|----------|
| `NoTimeout` | Messages never expire. |
| `Delete` | Expired messages are permanently removed. |
| `PushQueue` | Expired messages are moved to another queue (specified by `TargetName`). |
| `PublishRouter` | Expired messages are published to a router (specified by `TargetName`). |

```csharp
cfg.Options.MessageTimeout = new MessageTimeoutStrategy
{
    MessageDuration = 60,
    Policy = MessageTimeoutPolicy.Delete
};
```

---

### Type

| Property | `Type` |
|----------|--------|
| Type | `QueueType` enum |
| Default | `RoundRobin` |

The delivery pattern for the queue. See [Queue Types](queue-types.md) for details.

| Value | Description |
|-------|-------------|
| `Push` | Fan-out to all consumers. |
| `RoundRobin` | Load-balanced to one consumer at a time. |
| `Pull` | Consumer-initiated fetch. |

---

### MessageLimit

| Property | `MessageLimit` |
|----------|----------------|
| Type | `int` |
| Default | `0` (unlimited) |

Maximum number of messages the queue can hold. When the limit is reached, the `LimitExceededStrategy` determines what happens.

---

### LimitExceededStrategy

| Property | `LimitExceededStrategy` |
|----------|-------------------------|
| Type | `MessageLimitExceededStrategy` enum |
| Default | `RejectNewMessage` |

| Value | Behavior |
|-------|----------|
| `RejectNewMessage` | The new message is rejected and a negative response is sent to the producer. |
| `DeleteOldestMessage` | The oldest message in the queue is removed to make room for the new one. |

---

### MessageSizeLimit

| Property | `MessageSizeLimit` |
|----------|---------------------|
| Type | `ulong` |
| Default | `0` (unlimited) |

Maximum allowed size in bytes for a single message. Messages exceeding this limit are rejected.

When a producer sends a message that exceeds this limit, the server returns `HorseResultCode.LimitExceeded` to the producer. The message is **not** stored and is **not** delivered to any consumer.

---

### ClientLimit

| Property | `ClientLimit` |
|----------|---------------|
| Type | `int` |
| Default | `0` (unlimited) |

Maximum number of consumers that can subscribe to the queue simultaneously.

When the limit is reached and a new client attempts to subscribe, the server returns `HorseResultCode.LimitExceeded` to the client. The subscription is rejected and the client is **not** added to the queue.

---

### DelayBetweenMessages

| Property | `DelayBetweenMessages` |
|----------|------------------------|
| Type | `int` |
| Unit | Milliseconds |
| Default | `0` (no delay) |

Adds a delay after sending each message to consumers. Useful for throttling delivery when `WaitForAcknowledge` is not used.

```csharp
cfg.Options.DelayBetweenMessages = 100; // 100ms between messages
```

---

### PutBack / PutBackDelay

| Property | `PutBack` |
|----------|-----------|
| Type | `PutBackDecision` enum |
| Default | `Regular` |

| Property | `PutBackDelay` |
|----------|----------------|
| Type | `int` |
| Unit | Milliseconds |
| Default | `0` (no delay) |

When a message is negatively acknowledged or the acknowledge timeout expires, the put-back decision determines what happens:

| Value | Behavior |
|-------|----------|
| `No` | The message is discarded permanently. |
| `Regular` | The message is placed back at the **end** of the queue. |
| `Priority` | The message is placed back at the **front** of the queue (priority re-delivery). |

`PutBackDelay` adds a waiting period before the message is placed back, preventing hot loops.

```csharp
cfg.Options.PutBack = PutBackDecision.Regular;
cfg.Options.PutBackDelay = 500; // wait 500ms before put-back
```

---

### AutoDestroy

| Property | `AutoDestroy` |
|----------|---------------|
| Type | `QueueDestroy` enum |
| Default | `Disabled` |

Controls automatic queue destruction.

| Value | Behavior |
|-------|----------|
| `Disabled` | Queue is never automatically destroyed. |
| `NoMessages` | Queue is destroyed when it becomes empty and there are no in-flight deliveries (even if consumers are subscribed). All connected consumers are silently unsubscribed — their subscription references are cleared server-side. The consumers remain connected to the server but are no longer associated with the queue. |
| `NoConsumers` | Queue is destroyed when the last consumer unsubscribes (even if messages remain). All remaining messages in the queue (both regular and priority stores) are permanently destroyed. For persistent queues, the database files are also deleted. |
| `Empty` | Queue is destroyed when it has no messages, no in-flight deliveries, **and** no consumers. |

---

### AutoQueueCreation

| Property | `AutoQueueCreation` |
|----------|---------------------|
| Type | `bool` |
| Default | `true` |

When `true`, the server automatically creates a queue when a client tries to push or subscribe to a queue that doesn't exist yet. The new queue uses default options.

---

### MessageIdUniqueCheck

| Property | `MessageIdUniqueCheck` |
|----------|------------------------|
| Type | `bool` |
| Default | `false` |

When `true`, the server checks every incoming message ID for uniqueness and rejects duplicates. This has a small performance cost (~0.03 ms per message).

---

### Partition

| Property | `Partition` |
|----------|-------------|
| Type | `PartitionOptions` |
| Default | `null` |

Enables queue partitioning for multi-tenant or high-throughput scenarios. See [Partitioned Queues](partitioned-queues.md) for full details.

## Setting Default Options

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.RoundRobin;
        cfg.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        cfg.Options.CommitWhen = CommitWhen.AfterReceived;
        cfg.Options.AutoDestroy = QueueDestroy.Empty;
        cfg.Options.PutBack = PutBackDecision.Regular;
        cfg.Options.PutBackDelay = 1000;
        cfg.Options.MessageLimit = 10000;
        cfg.Options.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
    })
    .Build();
```

