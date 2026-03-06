# Overview

Horse Messaging queues provide a durable, high-throughput message delivery mechanism between producers and consumers. A **queue** is a named, ordered buffer that lives on the Horse server. Producers push messages into the queue, and the server delivers them to subscribed consumers according to the queue's type and options.

## Core Concepts

| Concept | Description |
|---------|-------------|
| **Queue** | A named message buffer on the server. Each queue has its own options, state, and subscriber list. |
| **Producer** | A client that sends (pushes) messages into a queue. |
| **Consumer** | A client that subscribes to a queue and receives messages from it. |
| **Message** | A unit of data flowing through the queue. Every message has a unique ID, optional headers, and a body (serialized content). |
| **Queue Manager** | The server-side component responsible for storing and delivering messages. Horse ships with two built-in managers: **Memory** and **Persistent**. |

## How It Works

```
Producer ──push──▶ [ Horse Server / Queue ] ──deliver──▶ Consumer(s)
                          │
                    ┌─────┴─────┐
                    │  Options  │  Type, Ack, Timeout, Limits …
                    │  Store    │  Memory or Persistent
                    │  State    │  Running, Paused, Stopped …
                    └───────────┘
```

1. A producer pushes a message to a queue (by name or by model type).
2. The server receives the message, optionally sends a **commit** back to the producer, and stores it in the queue's message store.
3. Depending on the queue type, the server delivers the message to one or more consumers.
4. Consumers process the message and optionally send an **acknowledge** (positive or negative) back to the server.
5. Based on the acknowledge decision, the server either removes the message, puts it back into the queue, or moves it to another queue.

For details on acknowledge decisions, commit behavior, and put-back policies, see [Acknowledgment & Reliability](acknowledgment.md).

## NuGet Packages

| Package | Purpose |
|---------|---------|
| `Horse.Messaging.Server` | Server-side queue engine, options, event handlers, authenticators |
| `Horse.Messaging.Client` | Client-side producer/consumer APIs, attributes, bus interfaces |
| `Horse.Messaging.Data` | Persistent queue storage backend (file-based) |
| `Horse.Messaging.Protocol` | Wire protocol, shared enums, message types |

## Minimal Example

### Server

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.RoundRobin;
        cfg.Options.Acknowledge = QueueAckDecision.waitForAcknowledge;
        cfg.UseMemoryQueues();
    })
    .Build();

HorseServer server = new HorseServer();
server.UseRider(rider);
server.Options.Hosts = [new HorseHostOptions { Port = 26222 }];
await server.StartAsync();
```

### Producer

```csharp
HorseClient client = new HorseClient();
client.Connect("horse://localhost:26222");

await client.Queue.Push(new OrderCreatedEvent { OrderId = 42 }, true, cancellationToken);
```

### Consumer

```csharp
[QueueName("OrderCreatedEvent")]
[QueueType(MessagingQueueType.RoundRobin)]
[AutoAck]
public class OrderCreatedConsumer : IQueueConsumer<OrderCreatedEvent>
{
    public Task Consume(HorseMessage message, OrderCreatedEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order received: {model.OrderId}");
        return Task.CompletedTask;
    }
}
```

Register the consumer on the client:

```csharp
var builder = Host.CreateDefaultBuilder(args);
builder.AddHorse(cfg =>
{
    cfg.AddHost("horse://localhost:26222");
    cfg.AddScopedConsumer<OrderCreatedConsumer>();
});
```

## Next Steps

- [Queue Types](queue-types.md) — Learn about Push, Round Robin, and Pull delivery patterns.
- [Queue Options](queue-options.md) — Full reference for every queue configuration option.
- [Server Configuration](server-configuration.md) — Setting up queues on the server side.
