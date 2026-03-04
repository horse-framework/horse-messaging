# Partitioned Queues

Partitioned queues split a single logical queue into multiple sub-queues (partitions), each identified by a **label**. This enables multi-tenant workloads, label-based routing, and horizontal scaling while maintaining per-partition ordering.

## How Partitions Work

```
              ┌─── Partition "tenant-1" ──▶ Consumer A
Producer ──▶ Parent Queue ─┤
              ├─── Partition "tenant-2" ──▶ Consumer B
              └─── Partition "tenant-3" ──▶ Consumer C
```

- A **parent queue** acts as a virtual router. It does not store messages itself.
- Messages are routed to **partition sub-queues** based on a label (typically sent as a header).
- Each partition is a real `HorseQueue` with its own message store, consumers, and processing loop.
- Partition sub-queues have `IsPartitionQueue = true` and do not have their own `PartitionManager`.
- Partition naming is deterministic: `{parentQueueName}-Partition-{label}` for labeled partitions, `{parentQueueName}-Partition-{counter}` for label-less ones. This ensures the same queue name across server restarts so that persistent `.hdb` files are naturally picked up.
- Per-partition FIFO ordering is guaranteed when using `WaitForAcknowledge` with a single consumer per partition. See [Acknowledgment & Reliability](acknowledgment.md#waitforacknowledge--behavior-by-queue-type).

## Enabling Partitions

### Server-Side Configuration

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.RoundRobin;
        cfg.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        cfg.Options.AutoQueueCreation = true;

        cfg.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            MaxPartitionCount = 100,
            SubscribersPerPartition = 1,
            AutoDestroy = PartitionAutoDestroy.NoMessages,
            AutoDestroyIdleSeconds = 60,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 3
        };

        cfg.UseMemoryQueues();
    })
    .Build();
```

### PartitionOptions Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Activates partitioning on the queue. |
| `MaxPartitionCount` | `int` | `0` (unlimited) | Maximum number of partition sub-queues. Applies to **both** labeled and label-less partitions. When the limit is reached, new labels are rejected (`LimitExceeded`) and new label-less subscribers cannot create additional partitions. Pushing to an **existing** label still works. `0` = unlimited. |
| `SubscribersPerPartition` | `int` | `1` | Maximum consumers per partition (maps to `ClientLimit` on each sub-queue). |
| `AutoDestroy` | `PartitionAutoDestroy` | `Disabled` | When to automatically destroy idle partitions. |
| `AutoDestroyIdleSeconds` | `int` | `30` | Idle time (in seconds) before the auto-destroy check triggers. |
| `AutoAssignWorkers` | `bool` | `false` | When `true`, label-less subscribers join a worker pool and are auto-assigned to partitions on demand. |
| `MaxPartitionsPerWorker` | `int` | `1` | Maximum partitions a single worker can serve simultaneously. `0` = unlimited. |

### PartitionAutoDestroy Values

| Value | Behavior |
|-------|----------|
| `Disabled` | Partitions are never automatically destroyed. |
| `NoMessages` | Partition is destroyed when it has no messages (queue is empty). |
| `NoConsumers` | Partition is destroyed when it has no subscribers. |
| `Empty` | Partition is destroyed when it has no messages **and** no consumers. |

---

## Usage Scenarios

### 1. Labeled — Dedicated Partition (Tenant Isolation)

Use when you want to fully isolate messages for a specific worker or tenant.

```csharp
// ── Server side ──────────────────────────────────────────
await rider.Queue.Create("FetchOrders", opts =>
{
    opts.Type = QueueType.Push;
    opts.Partition = new PartitionOptions
    {
        Enabled                = true,
        MaxPartitionCount      = 10,
        SubscribersPerPartition = 1,
        AutoDestroy            = PartitionAutoDestroy.NoConsumers,
        AutoDestroyIdleSeconds = 30
    };
});

// ── Worker (consumer) side ───────────────────────────────
await client.Queue.SubscribePartitioned(
    queue:                  "FetchOrders",
    partitionLabel:         "tenant-42",
    verifyResponse:         true,
    maxPartitions:          10,
    subscribersPerPartition: 1);

// ── Producer side ────────────────────────────────────────
await producer.Queue.Push("FetchOrders", message, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42") });
```

**What happens:**
1. `FetchOrders-Partition-tenant-42` is created for label `tenant-42` (on first connection).
2. Subsequent connections with the same label are routed to the same partition.
3. A message with header `Partition-Label: tenant-42` goes directly to this partition.
4. No other worker can see the message.
5. When the worker drops, the partition is removed after `AutoDestroyIdleSeconds` via the `NoConsumers` rule.

### 2. Label-less — Round-Robin Partition

Use when workers don't care which partition they belong to; messages are distributed round-robin across partitions with active subscribers.

```csharp
// ── Worker side ──────────────────────────────────────────
await client.Queue.Subscribe("JobQueue", true);

// ── Producer side ────────────────────────────────────────
await producer.Queue.Push("JobQueue", message, false);
// No PARTITION_LABEL header → round-robin
```

**What happens:**
1. Each worker gets its own partition when it connects.
2. Label-less messages are distributed **round-robin** across partitions with active subscribers.
3. When a worker drops, its partition is cleaned up via the `AutoDestroy` rule.

```
JobQueue (parent)
    ├── Partition-1  ← Worker-1  (round-robin target)
    └── Partition-2  ← Worker-2  (round-robin target)

Message (label-less) → Round-robin → Worker-1 or Worker-2
```

| State | Behavior |
|-------|----------|
| 3 workers connected, 100 messages | 3 partitions, messages round-robin distributed |
| 1 worker dropped | Other 2 continue; that worker's partition destroyed via `NoConsumers` |
| No subscribers, label-less push | Returns `NoConsumers` |

### 3. Auto-Assign Workers — Dynamic Tenant Scenario

Use when tenant/label values are not known at startup. Workers subscribe without a label and the server assigns them to partitions automatically as labeled messages arrive.

```csharp
// ── Server side ──────────────────────────────────────────
await rider.Queue.Create("OrderQueue", opts =>
{
    opts.Type = QueueType.Push;
    opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
    opts.Partition = new PartitionOptions
    {
        Enabled                 = true,
        MaxPartitionCount       = 0,     // unlimited — one per tenant
        SubscribersPerPartition = 1,
        AutoAssignWorkers       = true,  // ← enable worker pool
        MaxPartitionsPerWorker  = 10,    // ← each worker can serve up to 10 tenants
        AutoDestroy             = PartitionAutoDestroy.NoMessages,
        AutoDestroyIdleSeconds  = 60
    };
});

// ── Worker side (10 instances, no label) ─────────────────
await client.Queue.Subscribe("OrderQueue", true);
// Worker enters the available pool; server will assign it later

// ── Producer side ────────────────────────────────────────
await producer.Queue.Push("OrderQueue", order, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, tenantId) });
```

**What happens:**
1. 10 workers subscribe to `OrderQueue` without a label → they enter the **worker pool**.
2. First message arrives with `Partition-Label: tenant-42` → `OrderQueue-Partition-tenant-42` is created.
3. Partition has no subscriber → server pulls a worker from the pool and assigns it.
4. Since `MaxPartitionsPerWorker = 10`, the worker stays in the pool and can be assigned to 9 more partitions.
5. Next 9 tenants each get a partition and the **same worker** is assigned to all of them.
6. Worker-1 now serves 10 tenants concurrently — each partition has its own message processing loop.
7. 11th tenant triggers assignment of Worker-2 from the pool.
8. With `WaitForAcknowledge`, **per-tenant FIFO is still guaranteed** — each partition has its own ACK lock.
9. When all messages in a partition are consumed → `NoMessages` triggers destroy → worker's assignment count decreases → capacity freed.

```
OrderQueue (parent, AutoAssignWorkers=true, MaxPartitionsPerWorker=10)
  Pool: [Worker-2, Worker-3, ..., Worker-10]   ← 9 workers waiting
  ├── Partition-tenant-42 → Worker-1    ← 1/10 capacity used
  ├── Partition-tenant-99 → Worker-1    ← 2/10 capacity used
  ├── Partition-tenant-7  → Worker-1    ← 3/10 capacity used
  └── ... up to 10 partitions → Worker-1, then Worker-2 takes over
```

#### AutoDestroy and AutoAssignWorkers Compatibility

When `AutoAssignWorkers = true` and `MaxPartitionsPerWorker > 1`, the `AutoDestroy` value directly controls whether workers can be **recycled** to new partitions:

| AutoDestroy | Behavior with AutoAssignWorkers | Worker Recycling |
|-------------|--------------------------------|------------------|
| **`NoMessages`** | Partition destroyed when all messages are consumed. Worker's assignment count decreases, capacity freed for new partitions. | ✅ **Recommended** — workers are recycled as tenants finish their work |
| **`Disabled`** | Partitions never destroyed. Workers remain assigned forever, pool drains permanently. | ❌ **Bad** — worker pool exhausted, new tenants cannot be served |
| **`NoConsumers`** | Partitions destroyed only when worker disconnects. While worker is connected, partition lives. | ⚠️ **Ineffective** — same as Disabled while workers are healthy |
| **`Empty`** | Requires both no consumers AND no messages. While worker is connected, partition lives even if empty. | ⚠️ **Ineffective** — same as Disabled while workers are healthy |

> **Rule:** When using `AutoAssignWorkers + MaxPartitionsPerWorker > 1`, always use `AutoDestroy = NoMessages`. Other values prevent worker recycling.

### 4. Auto-Create via Client Subscribe

When `AutoQueueCreation` is enabled on the server, a client can create a partitioned queue on first subscribe:

```csharp
HorseResult result = await client.Queue.SubscribePartitioned(
    queue:                  "auto-part-q",   // doesn't exist yet
    partitionLabel:         "worker-1",
    verifyResponse:         true,
    maxPartitions:          10,              // → Partition-Limit: 10 header
    subscribersPerPartition: 1);             // → Partition-Subscribers: 1 header
// Server creates queue with PartitionOptions.Enabled=true, MaxPartitionCount=10

// To auto-create with unlimited partitions (override server default):
HorseResult r2 = await client.Queue.SubscribePartitioned(
    queue:          "unlimited-q",
    partitionLabel: "worker-1",
    verifyResponse: true,
    maxPartitions:  0);                      // → Partition-Limit: 0 header → unlimited
```

---

## Partition Labels

The partition label determines which sub-queue a message or subscriber is routed to. Labels are **case-insensitive** (`OrdinalIgnoreCase`).

When a message arrives at the parent queue, the server checks for the `Partition-Label` header:
- **Label present** → message is routed to the labeled partition (creates it if needed).
- **Label absent** → message is distributed round-robin across partitions with active subscribers.

> **Important:** A labeled message is **always** routed to its labeled partition, whether or not a subscriber is currently present. The message is stored and delivered when the owning worker connects. It is **never** cross-delivered to another worker.

---

## Routing Flow

```
Producer → Push("FetchOrders", msg, headers)
                │
          queue.IsPartitioned?
                │ YES
         PartitionManager.RouteMessage(msg)
                │
    PARTITION_LABEL header present?
    ┌───────────┴───────────────┐
   YES                          NO
    │                            │
GetOrCreate                 Round-Robin
LabelPartition(label)     (active partitions)
    │                            │
    └────────────────────────────┘
                │
           target.Push(msg)
```

After routing, the message's `Target` is rewritten to the partition sub-queue name. This is critical: consumer ACKs are built from `message.Target`, and without rewriting, the ACK would go to the parent queue — which has no matching delivery in its tracker.

The `Partition-Label` header is stripped from the message before forwarding to consumers. A `Partition-Id` header is stamped on the message to indicate which partition handled it.

> **MaxPartitionCount enforcement:** When `MaxPartitionCount > 0` and the limit is reached, a push with a **new** label returns `LimitExceeded` (the message is rejected). A push to an **existing** label still works — no new partition is created, the message goes to the existing one. Similarly, a subscribe with a new label returns `LimitExceeded`. This applies to both labeled and label-less partitions.

---

## Subscribing with Labels

### Using the `[PartitionedQueue]` Attribute

```csharp
// Dedicated partition for tenant-42
[QueueName("orders")]
[PartitionedQueue("tenant-42", MaxPartitions = 50, SubscribersPerPartition = 1)]
[AutoAck]
public class OrderConsumer : IQueueConsumer<OrderEvent>
{
    public Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

// Label-less partitioned subscribe (round-robin path)
[PartitionedQueue(MaxPartitions = 5)]
public class JobConsumer : IQueueConsumer<JobEvent> { ... }
```

| Syntax | Effect |
|--------|--------|
| `[PartitionedQueue("label")]` | Sends `Partition-Label` header on subscribe. `MaxPartitions` and `SubscribersPerPartition` not sent (server default used). |
| `[PartitionedQueue("label", MaxPartitions = N)]` | Also sends `Partition-Limit: N` for auto-create. `N = 0` means unlimited. |
| `[PartitionedQueue("label", MaxPartitions = N, SubscribersPerPartition = M)]` | Sends all three partition headers |
| `[PartitionedQueue]` or `[PartitionedQueue(null)]` | Label-less partitioned subscribe (round-robin) |

> **Attribute default values:** `MaxPartitions` and `SubscribersPerPartition` default to `-1` which means "not set — use server default". Setting `0` explicitly means **unlimited** (no limit enforced). Any value `> 0` sets an explicit limit.

When `AutoSubscribe = true`, the client automatically calls `SubscribePartitioned` with the declared values on every reconnect.

### Programmatic Subscribe

```csharp
await client.Queue.SubscribePartitioned("orders", "tenant-42", verifyResponse: true,
    maxPartitions: 50, 
    subscribersPerPartition: 1);
```

> **API note:** `maxPartitions` and `subscribersPerPartition` are `int?` (nullable). `null` = not sent (server default used), `0` = unlimited, `> 0` = explicit limit.

**Subscribe response headers:**
```
HorseResultCode: Ok
Partition-Id:    tenant-42
Queue-Name:      orders-Partition-tenant-42
```

### Consumer Registration with Partition (DI)

```csharp
services.AddHorseClient(b => b
    .AddHost("horse://localhost:2626")
    // No partition
    .AddSingletonConsumer<OrderConsumer>()
    // Explicit partition label (overrides any attribute)
    .AddSingletonConsumer<OrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)
    // Unlimited partitions (overrides server default)
    .AddSingletonConsumer<OrderConsumer>("tenant-42", maxPartitions: 0)
    // Enter the auto-assign worker pool (label-less, server assigns partitions on demand)
    .AddSingletonConsumer<JobConsumer>(queueNameTransform: n => n, enterWorkerPool: true));

// Transient and Scoped also supported:
.AddTransientConsumer<OrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)
.AddScopedConsumer<OrderConsumer>("tenant-42", maxPartitions: 10, subscribersPerPartition: 1)
```

> **Parameter types:** `maxPartitions` and `subscribersPerPartition` are `int?` (nullable). `null` (default) = not sent (server default used), `0` = unlimited, `> 0` = explicit limit.

> **Note:** The `partitionLabel` parameter cannot be `null` or empty string on the `queueNameTransform` overloads — it throws `ArgumentException`. To enter the worker pool without a label, use the `enterWorkerPool` overload instead.

Priority: **builder parameter > `[PartitionedQueue]` attribute > no partition**.

### DI with Name Transform

For advanced scenarios where consumers need to subscribe with a name transform (e.g., appending a suffix to queue names):

```csharp
// With explicit partition label
cfg.AddScopedConsumer<OrderConsumer>(
    queueNameTransform: queueName => $"{queueName}-free",
    partitionLabel: "tenant-42"
);

// Enter worker pool (label-less, server assigns on demand)
cfg.AddScopedConsumer<OrderConsumer>(
    queueNameTransform: queueName => $"{queueName}-free",
    enterWorkerPool: true
);
```

> **Note:** When using the `queueNameTransform` + `partitionLabel` overload, the label **cannot** be null or empty — this throws `ArgumentException`. Use `enterWorkerPool: true` to subscribe without a label.

---

## Sending Messages to Partitions

### Using `partitionLabel` Parameter (Recommended)

All `Push` methods on `IHorseQueueBus` accept an optional `partitionLabel` parameter. When set, the bus automatically adds the `Partition-Label` header:

```csharp
// Model → tenantId partition
await bus.Push(new OrderEvent { OrderId = 1 }, partitionLabel: tenantId);

// Model → explicit queue + partition
await bus.Push("FetchOrders", new OrderEvent { OrderId = 1 }, partitionLabel: tenantId);

// WaitForCommit + partition
HorseResult result = await bus.Push("FetchOrders", model, waitForCommit: true, partitionLabel: tenantId);

// No partition label — same as before
await bus.Push("FetchOrders", model, waitForCommit: false);

// partitionLabel: null → round-robin across partitions
await bus.Push("JobQueue", stream, waitForCommit: false, partitionLabel: null);
```

### Using Headers Directly (Low-Level)

```csharp
// Labeled
await producer.Queue.Push("FetchOrders", content, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42") });

// Label-less → round-robin across partitions
await producer.Queue.Push("FetchOrders", content, false);
```

---

## QueueType and Partition Behavior

The `QueueType` set on the parent queue is **propagated to partition sub-queues via `CloneFrom`**. The queue type takes effect inside each partition:

```
Producer → Push("FetchOrders", msg)
                │
         PartitionManager.RouteMessage  ← selection by label or round-robin
                │
         target = Partition-tenant-42
                │
         target.Push(msg)  ← QueueType kicks in FROM HERE
```

| QueueType | Behavior inside partition | When to use |
|-----------|--------------------------|-------------|
| `Push` | All subscribers receive simultaneously (broadcast) | Redundancy + isolation per partition |
| `RoundRobin` | Next subscriber receives (1-to-1 rotation) | Tenant with multiple workers |
| `Pull` | Stored; only delivered on explicit Pull request | Batch jobs, not-ready-yet consumers |

### With SubscribersPerPartition

| QueueType | SubscribersPerPartition = 1 | SubscribersPerPartition > 1 |
|-----------|-----------------------------|------------------------------|
| `Push` | Single worker receives | All receive (broadcast) |
| `RoundRobin` | Single worker receives | Next receives (1-to-1) |
| `Pull` | Waits for pull | Waits for pull |

### Practical Recommendations

| Need | Type | SubscribersPerPartition |
|------|------|-------------------------|
| Tenant isolation, single processor | `Push` or `RoundRobin` | 1 |
| Tenant isolation, load sharing | `RoundRobin` | N |
| Broadcast / replication within partition | `Push` | N |
| Store first, pull when ready | `Pull` | 1+ |

---

## Max Partitions Per Worker

`MaxPartitionsPerWorker` controls how many partitions a single worker can serve concurrently:

| Value | Behavior |
|-------|----------|
| `0` | Unlimited. A worker can serve any number of partitions. |
| `1` (default) | One partition at a time. When the partition is destroyed or the worker is unassigned, it becomes available for another partition. |
| `N` | Up to N partitions concurrently. Each partition has its own processing loop, so the worker processes them independently. |

With `WaitForAcknowledge`, per-partition FIFO ordering is still guaranteed even when a worker serves multiple partitions — because each partition has exactly one consumer. See [Acknowledgment & Reliability](acknowledgment.md#waitforacknowledge--behavior-by-queue-type).

---

## WaitForAcknowledge and Ordering Guarantees

`WaitForAcknowledge` guarantees that a single partition queue will not deliver message N+1 until message N is acknowledged. **However, partitioning by definition splits messages across multiple independent queues.** Each partition has its own acknowledge lock — they don't coordinate with each other.

### Label-less (Round-Robin) + WaitForAcknowledge

```
Parent: OrderQueue (Partitioned, WaitForAcknowledge)
  ├── Partition-1 → Worker-1
  └── Partition-2 → Worker-2

msg-1 → round-robin → Partition-1 → Worker-1 (processing...)
msg-2 → round-robin → Partition-2 → Worker-2 (processing in parallel!)
```

**Global ordering is NOT guaranteed in label-less mode.** msg-1 and msg-2 run in parallel on different partitions. However, if messages are sent **with a label**, all messages sharing the same label are routed to the same partition — and within that partition, FIFO ordering is guaranteed. See [Labeled + WaitForAcknowledge + SubscribersPerPartition = 1](#labeled--waitforacknowledge--subscribersperpartition--1) below.

If you need strict **global** ordering across all messages regardless of label, use a single (non-partitioned) queue with a single subscriber.

### Labeled + WaitForAcknowledge + SubscribersPerPartition = 1

```
Parent: OrderQueue (Partitioned, WaitForAcknowledge)
  ├── Partition-tenantA → Worker-A
  └── Partition-tenantB → Worker-B

msg-1 (label=A) → Partition-A → Worker-A (processing...)
msg-2 (label=A) → Partition-A → WAITS until msg-1 is acknowledged
msg-3 (label=B) → Partition-B → Worker-B (independent, runs in parallel)
```

**Per-label ordering IS guaranteed.** Within a single partition:
- Only one subscriber (`SubscribersPerPartition = 1`)
- `WaitForAcknowledge` lock blocks the next message until ACK
- Messages for `tenant-A` are always processed in FIFO order

This is the correct pattern for most real-world scenarios: you don't need global ordering — you need **per-tenant** or **per-entity** ordering.

### Ordering Decision Matrix

| Scenario | Partitioned? | Config | Ordering Guarantee |
|----------|-------------|--------|-------------------|
| Global strict ordering | ❌ No | Single queue, single subscriber, `WaitForAcknowledge` | ✅ Global FIFO |
| Per-tenant ordering | ✅ Yes | Labeled, `SubscribersPerPartition = 1`, `WaitForAcknowledge` | ✅ Per-label FIFO |
| Per-tenant ordering + load sharing | ✅ Yes | Labeled, `SubscribersPerPartition > 1`, `QueueType.RoundRobin` | ⚠️ Per-partition round-robin (no strict order) |
| Maximum throughput, no ordering needed | ✅ Yes | Label-less, round-robin | ❌ No ordering |

### Recommended Pattern

```csharp
// Per-tenant strict ordering
opts.Type = QueueType.Push; // or RoundRobin — same with SubscribersPerPartition=1
opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
opts.Partition = new PartitionOptions
{
    Enabled                 = true,
    MaxPartitionCount       = 100,
    SubscribersPerPartition = 1,   // ← critical: one consumer per partition
    AutoDestroy             = PartitionAutoDestroy.NoConsumers,
    AutoDestroyIdleSeconds  = 30
};

// Producer: always send with a label that represents the ordering domain
await bus.Push("OrderQueue", order, partitionLabel: order.TenantId);

// Consumer: subscribe with the tenant label
await client.Queue.SubscribePartitioned("OrderQueue", "tenant-42", true);
```

For dynamic tenant scenarios where partition queues are created on demand (e.g., tenant IDs are not known at server startup), `SubscribersPerPartition = 1` can also be enforced from the **subscriber side** — you don't need to configure it only on the server. The subscriber can set it via the `subscribersPerPartition` parameter or the `[PartitionedQueue]` attribute:

```csharp
// Option 1: Programmatic subscribe — subscriber declares subscribersPerPartition
await client.Queue.SubscribePartitioned("OrderQueue", "tenant-42",
    verifyResponse: true,
    maxPartitions: 100,
    subscribersPerPartition: 1);   // ← enforces single consumer per partition

// Option 2: Attribute on consumer class
[QueueName("OrderQueue")]
[PartitionedQueue("tenant-42", MaxPartitions = 100, SubscribersPerPartition = 1)]
public class OrderConsumer : IQueueConsumer<OrderEvent> { ... }
```

When the queue is auto-created via `AutoQueueCreation`, these values are sent as `Partition-Limit` and `Partition-Subscribers` headers and the server uses them to configure the partition options. This means FIFO guarantee per tenant can be established entirely from the client side without any server-side pre-configuration.

> **Key insight:** Partition = parallelism boundary. WaitForAcknowledge = ordering boundary within a partition. Use labels to define your ordering domain (tenant, customer, entity ID, etc.).

---

## Partition Auto Destroy

Partitions can be automatically destroyed when they become idle:

```csharp
cfg.Options.Partition = new PartitionOptions
{
    AutoDestroy = PartitionAutoDestroy.NoMessages,
    AutoDestroyIdleSeconds = 30
};
```

The auto-destroy timer checks at the specified interval. When the condition is met, the partition sub-queue is removed. When a partition is destroyed with `AutoAssignWorkers` enabled, the assigned worker's assignment count is decremented, and if the worker has capacity, it is returned to the pool for future assignments.

| Rule | Condition | Effect |
|------|-----------|--------|
| `Disabled` | — | Never removed |
| `NoConsumers` | No subscribers remain | Removed after `AutoDestroyIdleSeconds` |
| `NoMessages` | Queue drained (empty) | Removed after `AutoDestroyIdleSeconds` |
| `Empty` | No subscribers **and** no messages | Removed after `AutoDestroyIdleSeconds` |

> **Note:** Auto-destroy applies only to individual partitions, not to the parent queue. Other partitions are unaffected.

---

## Comparison with RoundRobin Queue

| | RoundRobin Queue | Partition (label-less) | Partition (labeled) |
|---|---|---|---|
| Physical structure | 1 queue, N workers | N queues, N workers | N queues, N workers |
| `WaitForAck` isolation | ⚠️ Busy worker skipped; message goes elsewhere | ✅ Each worker independent | ✅ Message stays in its partition |
| Tenant isolation | ❌ None | ❌ None | ✅ Full isolation with label |
| When worker drops | Messages go to other workers | Round-robin continues with remaining | Only that partition waits |
| Memory | Least | Medium (N queues) | Medium (N queues) |
| When to use | Light, fast jobs | Safer distribution | Tenant/worker isolation |

### The Truth About WaitForAcknowledge

> **Misconception:** `WaitForAcknowledge` guarantees ordered processing.
> **Reality:** It guarantees safe delivery of a single message. It is NOT a global ordering guarantee.

**RoundRobin + WaitForAck:** busy worker is skipped, message goes to the next — system does not block.

**Partition + WaitForAck:**
```
Message with label "tenant-42"
    → Sent to Partition-tenant-42
    → Worker-42 busy → message WAITS in Partition-tenant-42
    → Does NOT go to another worker  ← true isolation
```

### When to Use Which?

| Need | Preference |
|------|------------|
| Simple load distribution, fast jobs | RoundRobin |
| Message must not go to another worker when busy | **Partition (labeled)** |
| Full isolation per tenant/customer | **Partition (labeled)** |
| Messages processed in order by the same worker | **Partition (labeled, SubscribersPerPartition=1)** |
| Worker drop should only affect itself | **Partition** |
| Minimum memory, simplest setup | RoundRobin |

---

## Consumer Bounce and Reconnect Behavior

### Consumer Offline → Reconnects

| State | Behavior |
|-------|----------|
| Consumer **online** | Message routed to labeled partition → consumer, immediate delivery |
| Consumer **offline**, labeled push | Routed to **same labeled partition**, waits in message store |
| Consumer **reconnects** | Partition queue triggers delivery of buffered messages |

**Labeled messages never leave their partition; they are never dropped.**

```
Worker-A online:   msg(label=A) → Partition-A → Worker-A  ✓ isolated
Worker-A offline:  msg(label=A) → Partition-A store        ✓ still isolated
Worker-A returned: Partition-A → Worker-A                  ✓ correct delivery
                              Worker-B never sees A's messages  ✓
```

### Server Restart — Full Recovery (Persistent Queues)

When using persistent queues, all partition state survives a server restart:

| Data | How |
|------|-----|
| Parent queue `PartitionOptions` | `queues.json` |
| Partition sub-queue names and metadata | `queues.json` → `SubPartition` field |
| `IsPartitionQueue` flag | Set during re-attach when `SubPartition != null` |
| Partition label index | Re-attach → `_labelIndex` |
| Messages in sub-queues | `.hdb` files reloaded |
| Consumer reconnects with same label | **Same partition queue** — no new ID generated |
| Buffered messages after restart | Trigger → delivered |

```
Before restart:
  FetchOrders                     (Partition.Enabled=true)
  FetchOrders-Partition-tenant-42 (label=tenant-42, 5 messages in .hdb)

After restart:
  Pass 1 — all queues loaded from queues.json
  Pass 2 — SubPartition records re-attached to parent's PartitionManager

  FetchOrders                     ← PartitionManager.Partitions populated ✓
  FetchOrders-Partition-tenant-42 ← _labelIndex["tenant-42"] ✓, 5 messages ✓

Consumer reconnects with tenant-42:
  → existing Partition-tenant-42 ✓ (no new ID)
  → Trigger() → 5 buffered messages delivered ✓
```

---

## Partition Events

### Server-Side Event Handlers

```csharp
public class MyPartitionHandler : IPartitionEventHandler
{
    public Task OnPartitionCreated(HorseQueue parent, PartitionEntry entry)
    {
        Console.WriteLine($"[{parent.Name}] New partition: {entry.PartitionId} label={entry.Label}");
        return Task.CompletedTask;
    }
    
    public Task OnPartitionDestroyed(HorseQueue parent, string partitionId)
    {
        Console.WriteLine($"[{parent.Name}] Partition removed: {partitionId}");
        return Task.CompletedTask;
    }
}

rider.Queue.PartitionEventHandlers.Add(new MyPartitionHandler());
```


---

## Partition Metrics

You can inspect partition state at runtime through the `PartitionManager`:

```csharp
HorseQueue parentQueue = rider.Queue.Find("orders");
var manager = parentQueue.PartitionManager;

foreach (var snapshot in manager.GetMetrics())
{
    Console.WriteLine($"Partition: {snapshot.PartitionId}");
    Console.WriteLine($"  Label: {snapshot.Label ?? "(unlabeled)"}");
    Console.WriteLine($"  Queue Name: {snapshot.QueueName}");
    Console.WriteLine($"  Messages: {snapshot.MessageCount}");
    Console.WriteLine($"  Consumers: {snapshot.ConsumerCount}");
    Console.WriteLine($"  Created: {snapshot.CreatedAt}");
    Console.WriteLine($"  Last Message: {snapshot.LastMessageAt}");
}
```

### PartitionMetricSnapshot Fields

| Field | Type | Description |
|-------|------|-------------|
| `PartitionId` | `string` | Unique partition identifier |
| `Label` | `string?` | Worker label (`null` = label-less) |
| `QueueName` | `string` | Physical sub-queue name |
| `MessageCount` | `int` | Messages currently in the queue |
| `ConsumerCount` | `int` | Active subscriber count |
| `CreatedAt` | `DateTime` | Partition creation time |
| `LastMessageAt` | `DateTime?` | Last message delivery time |

---

## Header Reference

| Header | Direction | Description |
|--------|-----------|-------------|
| `Partition-Label` | Client → Server | Routing label for subscribe or push |
| `Partition-Id` | Server → Client | Partition ID assigned in the subscribe response |
| `Partition-Limit` | Client → Server | Max partition count for auto-create. `0` = unlimited. Not sent when `null` (server default used). |
| `Partition-Subscribers` | Client → Server | Max subscribers per partition for auto-create |

---

## Load Distribution Advantages

### Classic Single Queue Problem

```
Queue → [msg1, msg2, msg3, msg4, msg5]
          ↓       ↓       ↓
       Worker1  Worker1  Worker2   ← unbalanced, race condition, lock
```

All consumers compete on the same queue. In `WaitForAcknowledge` mode, while one consumer is busy the others also wait.

### With the Partition System

```
FetchOrders (parent)
    ├── Partition-a → Worker-1  (only its own messages)
    ├── Partition-b → Worker-2  (only its own messages)
    └── Partition-c → Worker-3  (only its own messages)
```

| Advantage | Description |
|-----------|-------------|
| **Zero lock contention** | Each worker reads from its own partition; no waiting |
| **Message ownership** | In `WaitForAck` mode message stays in partition; never escapes to another worker |
| **Tenant isolation** | A heavy tenant does not affect other tenants' partitions |
| **Scalable** | New worker = new partition opened |
| **Flexible cleanup** | If one partition is destroyed, others keep running |
| **Transparent producer** | Producer still writes to `FetchOrders`; routing belongs to the system |

---

## Partition Queue Internal Options

When a partition sub-queue is created, certain options are automatically set:

```csharp
partitionOptions.ClientLimit       = SubscribersPerPartition;
partitionOptions.AutoQueueCreation = false;   // prevents recursive partitioning
partitionOptions.Partition         = null;     // prevents recursive partitioning
```

The sub-queue inherits all other options (QueueType, Acknowledge, CommitWhen, etc.) from the parent queue via `CloneFrom`.
