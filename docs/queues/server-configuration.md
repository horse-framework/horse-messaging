# Server Configuration

This guide covers how to set up and configure queues on the Horse Messaging server.

## Building the Server with Queues

Use `HorseRiderBuilder` to configure the queue subsystem:

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        // Set default options for all queues
        cfg.Options.Type = QueueType.RoundRobin;
        cfg.Options.Acknowledge = QueueAckDecision.waitForAcknowledge;
        cfg.Options.AutoQueueCreation = true;

        // Register a storage backend
        cfg.UseMemoryQueues();
    })
    .Build();

HorseServer server = new HorseServer();
server.UseRider(rider);
server.Options.Hosts = [new HorseHostOptions { Port = 26222 }];
await server.StartAsync();
```

## Default Queue Options

The `cfg.Options` property is a `QueueOptions` instance that acts as the template for every new queue. When a queue is created (either programmatically or via
auto-creation), it inherits these defaults. Individual queues can override any option.

```csharp
cfg.Options.Type = QueueType.Push;
cfg.Options.MessageLimit = 5000;
cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
```

See [Queue Options](queue-options.md) for the full reference.

## Creating Queues Programmatically

You can create queues at runtime using `QueueRider`:

```csharp
HorseQueue queue = await rider.Queue.Create("my-queue");
```

Or with custom options:

```csharp
HorseQueue queue = await rider.Queue.Create("my-queue", o =>
{
    o.Type = QueueType.RoundRobin;
    o.Acknowledge = QueueAckDecision.JustRequest;
    o.MessageLimit = 1000;
});
```

You can also assign a topic to the queue for topic-based routing via Routers:

```csharp
queue.Topic = "commerce";
```

See [Queue Topics](queue-topics.md) for details on topic-based routing and `TopicBinding`.

## Queue Event Handlers

Implement `IQueueEventHandler` to hook into queue lifecycle events:

```csharp
public class MyQueueEventHandler : IQueueEventHandler
{
    public Task OnCreated(HorseQueue queue)
    {
        Console.WriteLine($"Queue created: {queue.Name}");
        return Task.CompletedTask;
    }

    public Task OnRemoved(HorseQueue queue)
    {
        Console.WriteLine($"Queue removed: {queue.Name}");
        return Task.CompletedTask;
    }

    public Task OnConsumerSubscribed(QueueClient client)
    {
        Console.WriteLine($"Consumer subscribed: {client.Client.Name}");
        return Task.CompletedTask;
    }

    public Task OnConsumerUnsubscribed(QueueClient client)
    {
        Console.WriteLine($"Consumer unsubscribed: {client.Client.Name}");
        return Task.CompletedTask;
    }

    public Task OnStatusChanged(HorseQueue queue, QueueStatus from, QueueStatus to)
    {
        Console.WriteLine($"Queue {queue.Name}: {from} -> {to}");
        return Task.CompletedTask;
    }
}
```

Register it during configuration:

```csharp
cfg.EventHandlers.Add(new MyQueueEventHandler());
```

## Queue Message Event Handlers

Implement `IQueueMessageEventHandler` to track individual message events:

```csharp
public class MyMessageEventHandler : IQueueMessageEventHandler
{
    public Task OnProduced(HorseQueue queue, QueueMessage message, MessagingClient producer)
    {
        // Message was received from a producer
        return Task.CompletedTask;
    }

    public Task OnConsumed(HorseQueue queue, QueueMessage message, MessagingClient consumer)
    {
        // Message was sent to a consumer
        return Task.CompletedTask;
    }

    public Task OnAcknowledged(HorseQueue queue, QueueMessage message, MessagingClient consumer, bool positive)
    {
        // Acknowledge received (positive or negative)
        return Task.CompletedTask;
    }

    public Task OnTimedOut(HorseQueue queue, QueueMessage message)
    {
        // Message expired
        return Task.CompletedTask;
    }

    public Task OnRemoved(HorseQueue queue, QueueMessage message)
    {
        // Message was removed from the queue
        return Task.CompletedTask;
    }

    public Task OnException(HorseQueue queue, QueueMessage message, Exception exception)
    {
        // An error occurred during message processing
        return Task.CompletedTask;
    }
}
```

Register it:

```csharp
cfg.MessageHandlers.Add(new MyMessageEventHandler());
```

## Queue Authenticators

Implement `IQueueAuthenticator` to control access to queues:

```csharp
public class MyQueueAuthenticator : IQueueAuthenticator
{
    public Task<bool> Authenticate(HorseQueue queue, MessagingClient client)
    {
        // Return true to allow subscribe, false to deny
        return Task.FromResult(true);
    }
}
```

Register it:

```csharp
cfg.Authenticators.Add(new MyQueueAuthenticator());
```

## Configuration Persistence

By default, queue configurations are persisted to a JSON file (`data/queues.json`). When the server restarts, it reloads the saved queue definitions.

You can customize or disable this:

```csharp
// Use a custom file path
cfg.UseCustomPersistentConfigurator(
    new QueueOptionsConfigurator(rider, "custom/path/queues.json")
);

// Or disable persistence entirely
cfg.UseCustomPersistentConfigurator(null);
```

