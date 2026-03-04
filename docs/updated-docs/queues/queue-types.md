# Queue Types

Horse Messaging supports three queue delivery patterns. The queue type determines **how messages are distributed** to consumers. The type must be set before the queue is initialized and cannot be changed at runtime.

## Push

```
Producer ──▶ Queue ──▶ Consumer A
                  ──▶ Consumer B
                  ──▶ Consumer C
```

Every message is delivered to **all** subscribed consumers. This is a fan-out / broadcast pattern.

- Each consumer receives every message.
- If there are no consumers, the message is stored in the queue until a consumer subscribes (depending on options).
- When `WaitForAcknowledge` is enabled with multiple consumers, the queue waits until **any one** consumer acks before dispatching the next message — it does not wait for all consumers. See [Acknowledgment & Reliability](acknowledgment.md#waitforacknowledge--behavior-by-queue-type) for details.
- Useful for notification broadcasts, event distribution, or replication scenarios.

**Server-side:**
```csharp
cfg.Options.Type = QueueType.Push;
```

**Client attribute:**
```csharp
[QueueType(MessagingQueueType.Push)]
public class NotificationConsumer : IQueueConsumer<Notification> { ... }
```

## Round Robin

```
Producer ──▶ Queue ──▶ Consumer A  (message 1)
                  ──▶ Consumer B  (message 2)
                  ──▶ Consumer A  (message 3)
                  ──▶ Consumer C  (message 4)
```

Each message is delivered to **exactly one** consumer, rotating through subscribed consumers. This is a load-balancing pattern.

- The server picks the next available consumer in round-robin order.
- If no consumers are available, the message stays in the queue.
- When `WaitForAcknowledge` is enabled, the server does **not** send a second message to a consumer that is still processing a previous one. A busy consumer is skipped and the next available consumer receives the message. This means:
  - **Single consumer:** Strict FIFO — one message at a time, next message waits until ack.
  - **Multiple consumers:** Each consumer processes one message at a time, but different consumers work **in parallel**. Queue-wide ordering is not guaranteed; per-consumer ordering is.
  - See [Acknowledgment & Reliability](acknowledgment.md#waitforacknowledge--behavior-by-queue-type) for full details.
- This is the **default** queue type.

**Server-side:**
```csharp
cfg.Options.Type = QueueType.RoundRobin;
```

**Client attribute:**
```csharp
[QueueType(MessagingQueueType.RoundRobin)]
public class OrderConsumer : IQueueConsumer<Order> { ... }
```

## Pull

```
Consumer A ──request──▶ Queue ──response──▶ Consumer A
Consumer B ──request──▶ Queue ──response──▶ Consumer B
```

Messages are **not pushed** to consumers. Instead, consumers explicitly request (pull) messages from the queue.

- The consumer sends a pull request specifying how many messages it wants.
- The server responds with the requested messages (or fewer if the queue doesn't have enough).
- Each pulled message is removed from the queue upon delivery.
- Useful for batch processing, rate-limited consumption, or worker-pool scenarios where consumers control their own pace.

**Server-side:**
```csharp
cfg.Options.Type = QueueType.Pull;
```

**Client-side pull request:**
```csharp
PullContainer container = await client.Queue.Pull(new PullRequest
{
    Queue = "jobs",
    Count = 10,
    Order = MessageOrder.FIFO
});

foreach (var message in container.ReceivedMessages)
{
    JobEvent job = message.Deserialize<JobEvent>(client);
    // process job
}
```

## Comparison

| Feature | Push | Round Robin | Pull |
|---------|------|-------------|------|
| Delivery target | All consumers | One consumer (rotating) | Requesting consumer |
| Delivery trigger | Server pushes on arrival | Server pushes on arrival | Consumer requests |
| Load balancing | No | Yes | Consumer-controlled |
| Message removed after | Sent to all | Acknowledged by one | Delivered to requester |
| Default | No | **Yes** | No |
| Supports `WaitForAcknowledge` | Yes | Yes (per-consumer) | No (ack is implicit) |

## Choosing a Type

- **Push** — When every subscriber needs to see every message (events, notifications, cache invalidation).
- **Round Robin** — When you want competing consumers that share the workload (task queues, order processing).
- **Pull** — When consumers need to control the pace of consumption (batch jobs, backpressure-sensitive workloads).

