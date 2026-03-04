# Queue Topics

A **topic** is a string label attached to a queue that enables **topic-based routing** via the Router system. Topics are used by `TopicBinding` to discover which queues should receive messages — the binding matches its target pattern against the `Topic` property of all queues on the server.

> **Important:** A topic has **no functional effect on its own**. It does not change how a queue pushes, subscribes, acknowledges, or stores messages. It is purely a metadata string on the queue. The **only** mechanism that reads and acts on topics is `TopicBinding` inside the Router system. Without a Router that has a `TopicBinding`, setting a topic on a queue does nothing — the value is stored and persisted but never used.

## How Topics Work

```
Producer → Router (TopicBinding, target: "commerce")
                │
         Filter.CheckMatch(queue.Topic, "commerce")
                │
    ┌───────────┴───────────┐
    ▼                       ▼
orders-queue             payments-queue
(Topic: "commerce")      (Topic: "commerce")
```

- Each `HorseQueue` has an optional `Topic` property (string, null by default).
- A `TopicBinding` in a Router scans all queues whose `Topic` matches the binding's `Target` pattern.
- Matching is **case-insensitive** and supports wildcard patterns (see [Pattern Matching](#pattern-matching) below).
- Topics do not affect normal queue push/subscribe operations — they are **only** relevant when a Router with a `TopicBinding` is configured. Without that, the topic is just inert metadata.

## Setting a Topic

### Server-Side (Programmatic)

```csharp
HorseQueue queue = await rider.Queue.Create("orders", o => o.Type = QueueType.Push);
queue.Topic = "commerce";
```

### Via Message Header (Auto-Create)

When a message is pushed to a non-existent queue with `AutoQueueCreation` enabled, the `Queue-Topic` header sets the topic during creation:

```csharp
var headers = new List<KeyValuePair<string, string>>
{
    new(HorseHeaders.QUEUE_TOPIC, "commerce")
};

await client.Queue.Push("orders", content, waitForCommit: true, messageHeaders: headers);
// Server auto-creates "orders" queue with Topic = "commerce"
```

### Using the `[QueueTopic]` Attribute (Client-Side)

Apply `[QueueTopic]` to a model class. When the first message of this type is pushed and the queue doesn't exist yet, the server auto-creates the queue with the declared topic:

```csharp
[QueueName("orders")]
[QueueTopic("commerce")]
public class OrderEvent
{
    public string OrderId { get; set; }
}
```

The attribute causes the `Queue-Topic` header to be included in the message. On the server side, `UpdateOptionsByMessage` reads this header and sets `queue.Topic`.

## Pattern Matching

`TopicBinding` uses `Filter.CheckMatch` for topic matching. The pattern is set as the binding's `Target`:

| Pattern | Match Type | Example | Matches |
|---------|-----------|---------|---------|
| `commerce` | Exact (case-insensitive) | `Commerce`, `COMMERCE` | ✅ |
| `comm*` | Starts with | `commerce`, `commodity` | ✅ |
| `*erce` | Ends with | `commerce`, `e-commerce` | ✅ |
| `*mmer*` | Contains | `commerce`, `e-commerce` | ✅ |

## Topic Binding in Routers

A `TopicBinding` is a special binding type in the Router system that routes messages to all queues matching a topic pattern.

### Creating a Topic Binding

```csharp
Router router = rider.Router.Add("order-router", RouteMethod.Distribute);

TopicBinding binding = new TopicBinding
{
    Name = "commerce-binding",
    Target = "commerce",           // matches queues with Topic == "commerce"
    RouteMethod = RouteMethod.Distribute,
    Interaction = BindingInteraction.None
};

router.AddBinding(binding);
```

### Route Methods with Topic Bindings

| Route Method | Behavior |
|-------------|----------|
| `Distribute` | Message is cloned and pushed to **all** matching queues |
| `RoundRobin` | Message is pushed to **one** matching queue in rotation |
| `OnlyFirst` | Message is pushed to the **first** matching queue only |

### Topic Binding Queue Cache

`TopicBinding` caches the matched queue list for **250ms** to avoid scanning all queues on every message. This means:
- Newly created queues with a matching topic may take up to 250ms to start receiving messages from an existing topic binding.
- Destroyed queues are cleaned up on the next cache refresh.

## Topic in Queue Information

The topic is included when listing queues via the client API:

```csharp
var result = await client.Queue.List();

foreach (var queue in result.Model)
{
    Console.WriteLine($"{queue.Name} → Topic: {queue.Topic ?? "(none)"}");
}
```

The `QueueInformation.Topic` field returns the current topic value (or `null` if not set).

## Topic Persistence

When using persistent queues, the topic is saved in `queues.json` as part of the `QueueConfiguration`:

```json
{
  "Name": "orders",
  "Topic": "commerce",
  "ManagerName": "Persistent",
  ...
}
```

On server restart, the topic is restored from configuration:

```csharp
if (!string.IsNullOrEmpty(cfg.Topic))
    queue.Topic = cfg.Topic;
```

## Updating and Clearing Topics

Topics can be changed or cleared at any time on the server side:

```csharp
// Update
queue.Topic = "new-topic";

// Clear
queue.Topic = null;
```

Changing a topic takes effect on the next `TopicBinding` cache refresh (~250ms). Messages already in the queue are not affected — topics only control **routing**, not message content.

## Multiple Queues with the Same Topic

Multiple queues can share the same topic. This is the primary use case — a `TopicBinding` with `RouteMethod.Distribute` sends the message to **all** queues with the matching topic:

```csharp
// Server setup: three queues with the same topic
await rider.Queue.Create("orders-us", o => o.Type = QueueType.Push);
await rider.Queue.Create("orders-eu", o => o.Type = QueueType.Push);
await rider.Queue.Create("orders-audit", o => o.Type = QueueType.Push);

rider.Queue.Find("orders-us").Topic = "orders";
rider.Queue.Find("orders-eu").Topic = "orders";
rider.Queue.Find("orders-audit").Topic = "orders";

// Router with topic binding
Router router = rider.Router.Add("order-router", RouteMethod.Distribute);
router.AddBinding(new TopicBinding
{
    Name = "order-topic",
    Target = "orders",
    RouteMethod = RouteMethod.Distribute
});

// Producer publishes to the router — message goes to all three queues
await client.Router.Publish("order-router", message, waitForAcknowledge: true);
```

## Summary

| Aspect | Detail |
|--------|--------|
| Property | `HorseQueue.Topic` (`string`, default `null`) |
| Set via | Server-side code, `Queue-Topic` header, `[QueueTopic]` attribute |
| Used by | `TopicBinding` in the Router system |
| Match logic | Case-insensitive, supports `*` wildcard (starts-with, ends-with, contains) |
| Persistence | Saved in `queues.json`, restored on restart |
| Cache | `TopicBinding` caches matched queues for 250ms |
| Effect on normal push/subscribe | **None** — without a Router + `TopicBinding`, topics are inert metadata |

