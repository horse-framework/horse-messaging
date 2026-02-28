# Queue Partition System — Reference

> **Last updated:** February 28, 2026
> This document covers every usage scenario, design decision, and API detail related to the partition system.
> Update both this file and `queue-partition-system-summary_TR.md` whenever a new feature is added.

---

## Table of Contents

1. [What We Built](#what-we-built)
2. [Core Concepts](#core-concepts)
3. [Partition Options](#partition-options)
4. [Usage Scenarios](#usage-scenarios)
   - [Labeled Usage — Dedicated Partition](#1-labeled-usage--dedicated-partition)
   - [Label-less + Orphan Enabled — Load Distribution](#2-label-less--orphan-enabled--load-distribution)
   - [Label-less + Orphan Disabled — Round-Robin Partition](#3-label-less--orphan-disabled--round-robin-partition)
   - [Partition Creation via AutoQueueCreation](#4-partition-creation-via-autoqueuecreation)
5. [QueueType and Partition Behaviour](#queuetype-and-partition-behaviour)
6. [Header Reference](#header-reference)
7. [Routing Flow](#routing-flow)
8. [Orphan Partition](#orphan-partition)
9. [AutoDestroy](#autodestroy)
10. [Metrics](#metrics)
11. [Event System](#event-system)
12. [Client API Summary](#client-api-summary)
    - [SubscribePartitioned](#subscribepartitioned-queueoperator)
    - [IHorseQueueBus Partition Push Overloads](#ihorsequeuebus--partition-push-overloads)
13. [Load Distribution Advantages](#load-distribution-advantages)
14. [Comparison with RoundRobin Queue](#comparison-with-roundrobin-queue)
15. [Known Behaviours and Notes](#known-behaviours-and-notes)
16. [Restart and Consumer-Bounce Behaviours](#restart-and-consumer-bounce-behaviours)
17. [What We Gained](#what-we-gained)

---

## What We Built

A system that automatically splits a single `HorseQueue` into **physical sub-queues (partitions)**.
From the outside there is **one queue**; internally multiple partition queues are running.

```
Producer  ──►  FetchOrders (parent, IsPartitioned=true)
                     │
               PartitionManager
                ┌────┴────────────────┐
                ▼                     ▼
   FetchOrders-Partition-a3k9x   FetchOrders-Partition-Orphan
         (owned by Worker-1)          (for ownerless messages)
```

---

## Core Concepts

### Parent Queue

```
FetchOrders              ← single name, the only queue visible from outside
IsPartitioned = true
PartitionManager ≠ null
```

### Partition Queues (created automatically)

```
FetchOrders-Partition-a3k9x   ← owned by worker-1  (IsPartitionQueue=true)
FetchOrders-Partition-b7m2p   ← owned by worker-2  (IsPartitionQueue=true)
FetchOrders-Partition-Orphan  ← for ownerless messages
```

- Each partition queue is a regular `HorseQueue` with `IsPartitionQueue = true`.
- Partition queues do **not** have their own `PartitionManager` (`IsPartitioned = false`).
- Name format: `{parentQueueName}-Partition-{base62Id}`
- The orphan uses a fixed name: `{parentQueueName}-Partition-Orphan`

### PartitionManager

Lives inside the parent queue. Makes all routing, create, and destroy decisions.

| Responsibility | Description |
|---|---|
| Subscribe routing | Routes incoming clients to the appropriate partition |
| Message routing | Sends messages to the correct partition via label or round-robin |
| Partition lifecycle | Create, destroy, fire events |
| AutoDestroy timer | Periodically cleans up empty/ownerless partitions |
| Metrics | Point-in-time metric snapshot for each partition |

### PartitionEntry

Internal class representing each partition:

```csharp
public class PartitionEntry
{
    public string PartitionId  { get; set; }  // base62 unique id ("Orphan" is fixed)
    public string Label        { get; set; }  // null = label-less or orphan
    public bool   IsOrphan     { get; set; }
    public HorseQueue Queue    { get; set; }
    public DateTime CreatedAt  { get; set; }
    public DateTime LastMessageAt { get; set; }
}
```

---

## Partition Options

```csharp
opts.Partition = new PartitionOptions
{
    Enabled                = true,
    MaxPartitionCount      = 10,    // 0 = unlimited
    SubscribersPerPartition = 1,    // max subscribers per partition
    EnableOrphanPartition  = true,  // enable orphan partition?
    AutoDestroy            = PartitionAutoDestroy.Disabled,
    AutoDestroyIdleSeconds = 30     // AutoDestroy check interval (seconds)
};
```

| Field | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable partitioning |
| `MaxPartitionCount` | `0` | Maximum label partition count (0 = unlimited) |
| `SubscribersPerPartition` | `1` | Max subscribers per partition |
| `EnableOrphanPartition` | `true` | Create an orphan partition? |
| `AutoDestroy` | `Disabled` | Automatic removal rule |
| `AutoDestroyIdleSeconds` | `30` | AutoDestroy timer interval |

---

## Usage Scenarios

### 1. Labeled Usage — Dedicated Partition

**When to use:** When you want to fully isolate messages for a specific worker/tenant.

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
        EnableOrphanPartition  = true,
        AutoDestroy            = PartitionAutoDestroy.NoConsumers,
        AutoDestroyIdleSeconds = 30
    };
});

// ── Worker (consumer) side ───────────────────────────────
await client.Queue.SubscribePartitioned(
    queue:                  "FetchOrders",
    partitionLabel:         "tenant-42",   // ← label exclusive to this worker
    verifyResponse:         true,
    maxPartitions:          10,
    subscribersPerPartition: 1);

// Response headers contain partition info:
// Partition-Id: a3k9x
// Queue-Name:   FetchOrders-Partition-a3k9x

// ── Producer side ────────────────────────────────────────
await producer.Queue.Push("FetchOrders", message, false,
    new[]
    {
        new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42")
    });
```

**What happens:**
1. `FetchOrders-Partition-a3k9x` is created for label `tenant-42` (on first connection)
2. Subsequent connections with the same label are routed to the same partition
3. A message with header `PARTITION_LABEL: tenant-42` goes directly to this partition
4. No other worker can see the message
5. When the worker drops, the partition is removed after `AutoDestroyIdleSeconds` via the `NoConsumers` rule

**Relation with orphan:**
A labeled worker is also automatically subscribed to the orphan partition (when `EnableOrphanPartition=true`).
This allows label-less messages to be received by this worker as well.

---

### 2. Label-less + Orphan Enabled — Load Distribution

**When to use:** When workers don't care which partition they belong to; when you want messages distributed equally across all workers.

```csharp
// ── Worker side ──────────────────────────────────────────
await client.Queue.Subscribe("JobQueue", true);
// OR
await client.Queue.SubscribePartitioned(
    queue:         "JobQueue",
    partitionLabel: null,        // ← no label
    verifyResponse: true);

// ── Producer side ────────────────────────────────────────
await producer.Queue.Push("JobQueue", message, false);
// No PARTITION_LABEL header
```

**What happens:**
1. Each worker gets its own label-less partition when it connects
2. All workers are also automatically subscribed to the **orphan partition**
3. Label-less messages are sent to the orphan partition
4. The orphan distributes to all subscribers via push semantics

```
JobQueue (parent)
    ├── Partition-abc  ← Worker-1 (its own partition)
    ├── Partition-xyz  ← Worker-2 (its own partition)
    └── Orphan         ← Worker-1 + Worker-2 (both are here)

Message (label-less) → Orphan → Worker-1 or Worker-2
```

| State | Behaviour |
|---|---|
| 3 workers connected, 100 messages | 3 partitions open, messages pushed via orphan |
| 1 worker dropped | Other 2 continue; that worker's partition destroyed via `NoConsumers` |
| New worker added | 4th partition opened; new messages automatically routed there |

---

### 3. Label-less + Orphan Disabled — Round-Robin Partition

**When to use:** When you don't want an orphan partition and want messages distributed round-robin across partitions.

```csharp
await rider.Queue.Create("JobQueue", opts =>
{
    opts.Type = QueueType.Push;
    opts.Partition = new PartitionOptions
    {
        Enabled                = true,
        MaxPartitionCount      = 5,
        SubscribersPerPartition = 1,
        EnableOrphanPartition  = false,   // ← orphan disabled
        AutoDestroy            = PartitionAutoDestroy.NoConsumers,
        AutoDestroyIdleSeconds = 30
    };
});
```

**What happens:**
1. Worker-1 connects → `Partition-abc` created
2. Worker-2 connects → `Partition-abc` full → `Partition-xyz` created
3. Label-less message arrives → **round-robin** across partitions with active subscribers
4. No orphan is ever created

```
JobQueue (parent)
    ├── Partition-abc  ← Worker-1  (round-robin target)
    └── Partition-xyz  ← Worker-2  (round-robin target)
```

| | Orphan Enabled | Orphan Disabled |
|---|---|---|
| Message distribution | Push via orphan | Direct to partition via round-robin |
| Memory | +1 orphan queue | Only label partitions |
| When no subscriber | Goes to orphan | Returns `NoConsumers` |

---

### 4. Partition Creation via AutoQueueCreation

```csharp
rider.Queue.Options.AutoQueueCreation = true;

HorseResult result = await client.Queue.SubscribePartitioned(
    queue:                  "auto-part-q",   // doesn't exist yet
    partitionLabel:         "worker-1",
    verifyResponse:         true,
    maxPartitions:          10,              // → PARTITION_LIMIT header
    subscribersPerPartition: 1);             // → PARTITION_SUBSCRIBERS header
// Server creates queue with PartitionOptions.Enabled=true, MaxPartitionCount=10
```

---

## Header Reference

```csharp
HorseHeaders.PARTITION_LABEL       = "Partition-Label"
HorseHeaders.PARTITION_ID          = "Partition-Id"
HorseHeaders.PARTITION_LIMIT       = "Partition-Limit"
HorseHeaders.PARTITION_SUBSCRIBERS = "Partition-Subscribers"
```

| Header | Direction | Description |
|---|---|---|
| `Partition-Label` | Client → Server | Which label the subscribe or push belongs to |
| `Partition-Id` | Server → Client | Which partition was assigned in the subscribe response |
| `Partition-Limit` | Client → Server | Max partition count for AutoCreate |
| `Partition-Subscribers` | Client → Server | Max subscribers per partition for AutoCreate |

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
GetOrCreate                EnableOrphanPartition?
LabelPartition(label)    ┌───────┴───────┐
    │                   YES             NO
    ▼                GetOrCreate     Round-Robin
entry.Queue          Orphan()       (active partitions)
    │                    │
    └────────────────────┘
                │
           target.Push(msg)
```

**Key behaviour — label present but no subscriber:**

> A labeled message is ALWAYS routed to its labeled partition whether or not a subscriber is
> currently present. The message is stored and delivered when the owning worker reconnects.
> It is NEVER cross-delivered to another worker or to the orphan.

---

## Orphan Partition

The orphan partition is a fallback pool for label-less or ownerless messages.

### Creation Rules

| Condition | Status |
|---|---|
| `EnableOrphanPartition = false` | Never created |
| `EnableOrphanPartition = true` + any subscribe | Created lazily |
| `WaitForAcknowledge` queue + `EnableOrphanPartition = true` | Created eagerly in `InitializeQueue` |

### Subscriber Rules

- Orphan `ClientLimit = 0` (unlimited).
- Every labeled worker is automatically added to the orphan in addition to its label partition.
- Every label-less worker is also added directly to the orphan.
- `EnableOrphanPartition = false` → no orphan subscription.

### WaitForAcknowledge Guarantee

```csharp
if (Acknowledge == WaitForAcknowledge && !orphan.Clients.Any())
    return null; // → NoConsumers
```

---

## AutoDestroy

```csharp
public enum PartitionAutoDestroy
{
    Disabled,      // Never removed (default)
    NoConsumers,   // !Clients.Any()
    NoMessages,    // IsEmpty
    Empty          // !Clients.Any() && IsEmpty
}
```

| Rule | Condition | Behaviour |
|---|---|---|
| `Disabled` | — | Never removed |
| `NoConsumers` | No subscribers remain | Removed after `AutoDestroyIdleSeconds` |
| `NoMessages` | Queue drained | Removed after `AutoDestroyIdleSeconds` |
| `Empty` | No subscribers and no messages | Removed after `AutoDestroyIdleSeconds` |

- Orphan is **exempt** from AutoDestroy (`IsOrphan = true` entries are skipped).
- Parent queue keeps running; other partitions are unaffected.

---

## Metrics

```csharp
queue.Info.RefreshPartitionMetrics(queue.PartitionManager);

Console.WriteLine($"Partition count : {queue.Info.PartitionCount}");
Console.WriteLine($"Orphan active   : {queue.Info.OrphanPartitionActive}");

foreach (PartitionMetricSnapshot snap in queue.Info.PartitionMetrics)
{
    Console.WriteLine($"  [{snap.Label ?? "(orphan)"}] id={snap.PartitionId}" +
                      $"  messages={snap.MessageCount}  consumers={snap.ConsumerCount}");
}
```

### PartitionMetricSnapshot

| Field | Type | Description |
|---|---|---|
| `PartitionId` | `string` | Unique partition identifier |
| `Label` | `string?` | Worker label (null = label-less or orphan) |
| `IsOrphan` | `bool` | Is this the orphan partition? |
| `QueueName` | `string` | Physical queue name |
| `MessageCount` | `int` | Messages currently in the queue |
| `ConsumerCount` | `int` | Active subscriber count |
| `CreatedAt` | `DateTime` | Partition creation time |
| `LastMessageAt` | `DateTime` | Last message delivery time |

### QueueInfo Fields

```csharp
public int PartitionCount { get; set; }
public bool OrphanPartitionActive { get; set; }
public List<PartitionMetricSnapshot> PartitionMetrics { get; set; }
```

---

## Event System

```csharp
// Server-side
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

// Client-side
await client.Event.SubscribeToQueuePartitionCreated("FetchOrders", async (ev, c) =>
{
    Console.WriteLine($"New partition: {ev.Name} → {ev.Content}");
});
```

```csharp
HorseEventType.QueuePartitionCreated   // New partition created
HorseEventType.QueuePartitionDestroyed // Partition destroyed
```

---

## Client API Summary

### `SubscribePartitioned` (QueueOperator)

```csharp
Task<HorseResult> QueueOperator.SubscribePartitioned(
    string   queue,
    string   partitionLabel,              // null = label-less
    bool     verifyResponse,
    int      maxPartitions       = 0,     // PARTITION_LIMIT for AutoCreate
    int      subscribersPerPartition = 0, // PARTITION_SUBSCRIBERS for AutoCreate
    IEnumerable<KeyValuePair<string,string>> additionalHeaders = null);
```

| Header built | Condition |
|---|---|
| `Partition-Label` | `partitionLabel` not null |
| `Partition-Limit` | `maxPartitions > 0` |
| `Partition-Subscribers` | `subscribersPerPartition > 0` |

```csharp
// Tenant isolation
await client.Queue.SubscribePartitioned("FetchOrders", "tenant-42", true, 10, 1);

// Label-less, distribution via orphan
await client.Queue.SubscribePartitioned("JobQueue", null, true, 5, 1);

// Auto-create
await client.Queue.SubscribePartitioned("new-queue", "w1", true, maxPartitions: 8);
```

**Subscribe response:**
```
HorseResultCode: Ok
Partition-Id:    a3k9x
Queue-Name:      FetchOrders-Partition-a3k9x
```

---

### `IHorseQueueBus` — Partition Push Overloads

All methods automatically add the `PARTITION_LABEL` header.

#### `PushToPartition` — raw / string content

```csharp
Task<HorseResult> PushToPartition(
    string queue, string partitionLabel, MemoryStream content,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushToPartition(
    string queue, string partitionLabel, string content,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushToPartition(
    string queue, string partitionLabel, MemoryStream content,
    string messageId, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushToPartition(
    string queue, string partitionLabel, string content,
    string messageId, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
```

#### `PushJsonToPartition` — JSON / model object

```csharp
Task<HorseResult> PushJsonToPartition(
    string partitionLabel, object jsonObject,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushJsonToPartition(
    string queue, string partitionLabel, object jsonObject,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushJsonToPartition(
    string partitionLabel, object jsonObject, string messageId,
    bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

Task<HorseResult> PushJsonToPartition(
    string queue, string partitionLabel, object jsonObject,
    string messageId, bool waitForCommit = false,
    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
```

#### Usage Examples

```csharp
// String message → tenantId partition
await bus.PushToPartition("FetchOrders", tenantId, "payload");

// JSON model → tenantId partition (queue name from [QueueName])
[QueueName("FetchOrders")]
public record FetchOrderEvent(string OrderId);
await bus.PushJsonToPartition(tenantId, new FetchOrderEvent("ord-1"));

// JSON model → explicit queue + messageId
await bus.PushJsonToPartition("FetchOrders", tenantId,
    new FetchOrderEvent("ord-1"), messageId: Guid.NewGuid().ToString());

// WaitForCommit
HorseResult result = await bus.PushToPartition("FetchOrders", tenantId, "payload",
    waitForCommit: true);

// Label null → orphan (or round-robin if orphan disabled)
await bus.PushToPartition("JobQueue", null, "unlabeled-payload");
```

### Low-level (via QueueOperator)

```csharp
// Labeled
await producer.Queue.Push("FetchOrders", content, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42") });

// Label-less → orphan (enabled) or round-robin (disabled)
await producer.Queue.Push("FetchOrders", content, false);
```

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
    ├── Partition-c → Worker-3  (only its own messages)
    └── Orphan      → Worker-1 + Worker-2 + Worker-3 (label-less fallback)
```

| Advantage | Description |
|---|---|
| **Zero lock contention** | Each worker reads from its own partition; no waiting |
| **Message ownership** | In `WaitForAck` mode message stays in partition; never escapes to another worker |
| **Tenant isolation** | A heavy tenant does not affect other tenants' partitions |
| **Scalable** | New worker = new partition opened |
| **Flexible cleanup** | If one partition is destroyed, others keep running |
| **Transparent producer** | Producer still writes to `FetchOrders`; routing belongs to the system |

---

## Comparison with RoundRobin Queue

### Side-by-Side

| | RoundRobin Queue | Partition (label-less + orphan) | Partition (labeled) |
|---|---|---|---|
| Physical structure | 1 queue, N workers | N+1 queues, N workers | N queues, N workers |
| `WaitForAck` isolation | ⚠️ Busy worker skipped; message goes elsewhere | ✅ Each worker independent | ✅ Message stays in its partition |
| Tenant isolation | ❌ None | ❌ None | ✅ Full isolation with label |
| When worker drops | Messages go to other workers | Continues via orphan | Only that partition waits |
| Memory | Least | Medium (+1 orphan) | Medium (N queues) |
| When to use | Light, fast jobs | Safer distribution | Tenant/worker isolation |

### The Truth About WaitForAcknowledge

> **Misconception:** `WaitForAcknowledge` guarantees ordered processing.
> **Reality:** It guarantees safe delivery of a single message. It is NOT an ordering guarantee.

**RoundRobin + WaitForAck:** busy worker is skipped, message goes to the next — system does not block.

**Partition + WaitForAck:**
```
Message with label "tenant-42"
    → Sent to Partition-tenant42
    → Worker-42 busy → message WAITS in Partition-tenant42
    → Does NOT go to another worker  ← true isolation
```

### When to Use Which?

| Need | Preference |
|---|---|
| Simple load distribution, fast jobs | RoundRobin |
| Message must not go to another worker when busy | **Partition (labeled)** |
| Full isolation per tenant/customer | **Partition (labeled)** |
| Messages processed in order by the same worker | **Partition (labeled, SubscribersPerPartition=1)** |
| Worker drop should only affect itself | **Partition** |
| Minimum memory, simplest setup | RoundRobin |

---

## QueueType and Partition Behaviour

The `QueueType` given to the parent queue is **propagated to partition queues via `CloneFrom`**.

```
Producer → Push("FetchOrders", msg)
               │
         PartitionManager.RouteMessage  ← selection by label or round-robin
               │
         target = Partition-a3k9x
               │
         target.Push(msg)  ← QueueType kicks in FROM HERE
```

| QueueType | Behaviour inside partition | When to use |
|---|---|---|
| `Push` | All subscribers receive simultaneously (broadcast) | Redundancy + isolation per partition |
| `RoundRobin` | Next subscriber receives (1-to-1 rotation) | Tenant with multiple workers |
| `Pull` | Stored; only delivered on explicit Pull request | Batch jobs, not-ready-yet consumers |

### With SubscribersPerPartition = 1

| QueueType | SubscribersPerPartition = 1 | SubscribersPerPartition > 1 |
|---|---|---|
| `Push` | Single worker receives | All receive (broadcast) |
| `RoundRobin` | Single worker receives | Next receives (1-to-1) |
| `Pull` | Waits for pull | Waits for pull |

### Practical Recommendations

| Need | Type | SubscribersPerPartition |
|---|---|---|
| Tenant isolation, single processor | `Push` or `RoundRobin` | 1 |
| Tenant isolation, load sharing | `RoundRobin` | N |
| Broadcast / replication within partition | `Push` | N |
| Store first, pull when ready | `Pull` | 1+ |

---

## Known Behaviours and Notes

### Label Matching
- Case-insensitive (`OrdinalIgnoreCase`).
- `_labelIndex` only stores non-null labels.

### Subscribe Order

```
SubscribeClient(client, label)
    ├─ label != null → GetOrCreateLabelPartition(label) → AddClient
    │                   If Full → SubscribeToOrphan(client)
    └─ label == null → Try existing non-orphan partitions
                       All Full → CreatePartition(null) → AddClient
                       MaxPartitionCount exceeded → SubscribeToOrphan(client)
    └─ EnableOrphanPartition == true → Also add to orphan (in both cases)
```

### Partition Queue Options

```csharp
partitionOptions.ClientLimit        = SubscribersPerPartition;
partitionOptions.AutoQueueCreation  = false;   // prevents recursive partitioning
partitionOptions.Partition          = null;    // prevents recursive partitioning

orphanOptions.ClientLimit = 0; // unlimited subscribers
```

### AutoDestroy and Orphan
- Orphan is **skipped** in AutoDestroy timer (`IsOrphan = true`) — never automatically removed.
- Orphan is only removed when `HorseQueue.Destroy()` is called directly.

---

## Restart and Consumer-Bounce Behaviours

> Tested by `PartitionRestartTest` and `PartitionPersistentTest`.

### Consumer Bounce (No Server Restart)

| State | Behaviour |
|---|---|
| Consumer **online** | Directly to labeled partition → consumer, immediate delivery |
| Consumer **offline**, labeled push | Routed to **same labeled partition**, waits in store |
| Consumer **reconnects** | Messages delivered via `Trigger()` |

**`enableOrphan` does not affect this.** Labeled messages never go to the orphan; they are never dropped.

```csharp
opts.Partition = new PartitionOptions
{
    Enabled               = true,
    EnableOrphanPartition = false,   // optional — not needed for labeled bounce protection
    MaxPartitionCount     = 10,
    SubscribersPerPartition = 1,
};
```

### Full Tenant Isolation

```
Worker-A online:   msg(label=A) → Partition-A → Worker-A  ✓ isolated
Worker-A offline:  msg(label=A) → Partition-A store        ✓ still isolated
Worker-A returned: Partition-A → Worker-A                  ✓ correct delivery
                              Worker-B never sees A's messages  ✓
```

### Server Restart — Full Recovery

#### Preserved and Correctly Restored

| Data | How |
|---|---|
| Parent queue `PartitionOptions` | `queues.json` |
| Partition sub-queue names and metadata | `queues.json` → `SubPartition` field |
| `IsPartitionQueue` flag | Set in ReAttach when `SubPartition != null` |
| Partition label index | ReAttach → `_labelIndex` |
| Orphan partition | ReAttach → `_orphanPartition` |
| Messages in sub-queues | `.hdb` files reloaded |
| Consumer reconnects with same label | **SAME partition queue** — no new GUID |
| Buffered messages after restart | `Trigger()` → delivered ✅ |

#### Post-Restart State Diagram

```
Before restart:
  FetchOrders                     (Partition.Enabled=true)
  FetchOrders-Partition-abc123    (label=tenant-42, 5 messages in .hdb)
  FetchOrders-Partition-Orphan    (IsOrphan=true, 3 messages in .hdb)

After restart:
  Pass 1 — all queues loaded from queues.json
  Pass 2 — SubPartition records ReAttached to parent's PartitionManager

  FetchOrders                     ← PartitionManager.Partitions populated ✓
  FetchOrders-Partition-abc123    ← _labelIndex["tenant-42"] ✓, 5 messages ✓
  FetchOrders-Partition-Orphan    ← _orphanPartition ✓, 3 messages ✓

Consumer reconnects with tenant-42:
  → existing Partition-abc123 ✓ (no new GUID)
  → Trigger() → 5 buffered messages delivered ✓
```

#### SubPartition Metadata in queues.json

```json
{
  "Name": "FetchOrders-Partition-abc123",
  "SubPartition": {
    "ParentQueueName": "FetchOrders",
    "PartitionId": "abc123",
    "Label": "tenant-42",
    "IsOrphan": false
  }
}
```

### Test Coverage

| Test Name | Scenario |
|---|---|
| `ConsumerBounce_OfflinePushedMessages_DeliveredOnReconnect` | Consumer drops → buffered → reconnects → delivered |
| `ServerRestart_ParentQueue_PartitionOptionsRestored` | PartitionOptions preserved after restart |
| `Producer_Continuous_ConsumerReconnects_ReceivesAll` | Producer keeps pushing → bounce → reconnect → all delivered |
| `TwoTenants_ConsumerBounce_FullIsolationMaintained` | A drops → A's messages wait → B never sees them → A returns → full delivery |
| `ServerRestart_PartitionSubQueues_ReAttachedAndMessagesDelivered` | Sub-queue re-attached, same GUID, message delivered |
| `OrphanPartition_ConsumerBounce_OfflineMessages_Delivered` | Orphan messages delivered after bounce |

---

## What We Gained

| Feature | Description |
|---|---|
| **Tenant isolation** | Each tenant's messages in their own partition; never affects others |
| **Lock-free scaling** | Workers = partitions; zero competition |
| **Dynamic expansion** | Add worker → partition opens; remove worker → partition closes |
| **Orphan guarantee** | Label-less messages never lost; automatic fallback in `WaitForAck` mode |
| **Partial fault tolerance** | One partition destroyed → others keep running |
| **Transparent producer** | Producer writes to `FetchOrders`; routing belongs to the system |
| **Observability** | Message count, consumer count, last message time per partition |
| **Auto-create** | `PARTITION_LIMIT` + `PARTITION_SUBSCRIBERS` headers create fully configured queue |
| **Full restart recovery** | Sub-queue names preserved, `.hdb` messages survive restart, no new GUIDs |
| **Guaranteed message buffering** | Labeled messages stored when consumer offline; delivered on reconnect |
