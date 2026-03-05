# Consuming Messages

Consumers subscribe to queues and receive messages delivered by the server. Horse Messaging provides a model-based consumer interface with dependency injection support.

## IQueueConsumer Interface

Implement `IQueueConsumer<TModel>` to define a consumer:

```csharp
public class OrderConsumer : IQueueConsumer<OrderCreatedEvent>
{
    public Task Consume(HorseMessage message, OrderCreatedEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Processing order: {model.OrderId}");
        return Task.CompletedTask;
    }
}
```

The server deserializes the message body into `TModel` and invokes `Consume`. The queue name is resolved from the model type or its `[QueueName]` attribute.

**Parameters:**

| Parameter | Description |
|-----------|-------------|
| `message` | The raw `HorseMessage` with headers and metadata. |
| `model` | The deserialized model instance. |
| `client` | The `HorseClient` connection. Used for sending ack/nack or other operations. |
| `cancellationToken` | Cancelled when the client disconnects or shuts down. Pass to async I/O calls. |

## Consumer Lifecycle

Consumers can be registered with three lifetimes:

| Lifetime | Behavior |
|----------|----------|
| **Transient** | A new consumer instance is created for every message. |
| **Scoped** | A new consumer instance is created per message within a DI scope. Other scoped services share the same scope. |
| **Singleton** | A single consumer instance handles all messages. |

## Registering Consumers

### With Dependency Injection (IServiceCollection)

```csharp
services.AddHorse(cfg =>
{
    cfg.AddHost("horse://localhost:26222");

    // Register individual consumers
    cfg.AddTransientConsumer<OrderConsumer>();
    cfg.AddScopedConsumer<PaymentConsumer>();
    cfg.AddSingletonConsumer<MetricsConsumer>();

    // Or register all consumers from an assembly
    cfg.AddScopedConsumers(typeof(OrderConsumer));
});
```

### With HorseClientBuilder (without DI)

```csharp
HorseClientBuilder builder = new HorseClientBuilder();
builder.AddHost("horse://localhost:26222");
builder.AddSingletonConsumer<OrderConsumer>();

HorseClient client = builder.Build();
client.Connect();
```

> **Note:** Without `IServiceCollection`, only **Singleton** lifetime is supported.

## Auto Subscribe

By default, `HorseClient.AutoSubscribe` is `true`. When the client connects (or reconnects), it automatically subscribes to all registered consumer queues. You can disable this:

```csharp
cfg.AutoSubscribe(false);
```

Then subscribe manually:

```csharp
await client.Queue.Subscribe("orders", true, cancellationToken);
```

## Manual Subscribe & Unsubscribe

```csharp
// Subscribe
HorseResult result = await client.Queue.Subscribe("orders", true, cancellationToken);

// Unsubscribe
HorseResult result = await client.Queue.Unsubscribe("orders", cancellationToken);
```

The `verifyResponse` parameter controls whether the client waits for a response from the server:

- **`true`**: The client waits for the server to confirm the operation. The returned `HorseResult` contains the actual result code (`Ok`, `Unauthorized`, `LimitExceeded`, `NotFound`, etc.). Use this when you need to verify the subscription succeeded.
- **`false`**: The client sends the request and returns immediately without waiting for a server response. The operation is fire-and-forget — you get no feedback about success or failure.

## Pull Requests

For `Pull`-type queues, consumers must explicitly request messages:

```csharp
PullContainer container = await client.Queue.Pull(new PullRequest
{
    Queue = "jobs",
    Count = 10,
    Order = MessageOrder.FIFO
}, cancellationToken);

foreach (HorseMessage message in container.ReceivedMessages)
{
    var job = message.Deserialize<JobEvent>(client);
    // process job
}
```

Pull request shortcuts:

```csharp
// Pull a single message
PullContainer single = await client.Queue.Pull(PullRequest.Single("jobs"), cancellationToken);
```

## Consumer Attributes

Apply attributes to consumer or model classes to configure queue behavior at subscribe time:

```csharp
[QueueName("orders")]
[QueueType(MessagingQueueType.RoundRobin)]
[AutoAck]
[AutoNack(NegativeReason.Error)]
[PutBack(PutBack.Regular, 1000)]
[AcknowledgeTimeout(30)]
[Retry(3, delayBetweenRetries: 200)]
[MoveOnError("orders-error")]
public class OrderConsumer : IQueueConsumer<OrderCreatedEvent>
{
    public Task Consume(HorseMessage message, OrderCreatedEvent model, HorseClient client,
        CancellationToken cancellationToken)
    {
        // process
        return Task.CompletedTask;
    }
}
```

### Attribute Reference

| Attribute | Description |
|-----------|-------------|
| `[QueueName("name")]` | Specifies the queue to subscribe to. Defaults to model type name. |
| `[QueueType(type)]` | Queue type hint for auto-created queues. |
| `[AutoAck]` | Automatically sends a positive acknowledge after `Consume` completes successfully. |
| `[AutoNack(reason)]` | Automatically sends a negative acknowledge if `Consume` throws an exception. |
| `[PutBack(decision, delayMs)]` | Put-back behavior for failed messages. |
| `[AcknowledgeTimeout(seconds)]` | Acknowledge timeout for the queue. |
| `[DelayBetweenMessages(ms)]` | Throttle delivery between messages. |
| `[MessageTimeout(policy, seconds)]` | Message expiration configuration. |
| `[Retry(count, delayMs)]` | Retry the consume operation up to `count` times with `delayMs` between retries. Max 100 retries. See [Retry & Redelivery](retry-redelivery.md). |
| `[MoveOnError("queue")]` | On unhandled exception, moves the message to the specified error queue. See [Dead-Letter Queues & Exception Handling](dead-letter-exceptions.md). |
| `[HighPriorityMessage]` | Marks messages of this type as high priority. |
| `[QueueManager("name")]` | Targets a specific storage backend. |
| `[QueueTopic("topic")]` | Sets a topic string on the queue. Used for topic-based routing. See [Queue Topics](queue-topics.md). |
| `[UniqueIdCheck]` | Enables message ID uniqueness checking. |
| `[PartitionedQueue("label")]` | Subscribes to a partitioned queue with the given label. See [Partitioned Queues](partitioned-queues.md). |

