using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

#region Test Models

public interface IEvent
{
    string EventId { get; set; }
}

public class OrderCreatedEvent : IEvent
{
    public string EventId { get; set; }
    public string OrderNumber { get; set; }
}

public class PaymentCompletedEvent : IEvent
{
    public string EventId { get; set; }
    public decimal Amount { get; set; }
}

[QueueName("OrderQueue")]
public class OrderCreatedWithQueueNameEvent : IEvent
{
    public string EventId { get; set; }
    public string OrderNumber { get; set; }
}

[QueueName("PaymentQueue")]
public class PaymentCompletedWithQueueNameEvent : IEvent
{
    public string EventId { get; set; }
    public decimal Amount { get; set; }
}

#endregion

#region Custom Serializers

/// <summary>
/// A naive custom serializer that serializes using standard System.Text.Json.
/// This should NOT affect queue routing because routing happens before serialization.
/// </summary>
public class NaiveCustomSerializer : IMessageContentSerializer
{
    public void Serialize(HorseMessage message, object model)
    {
        message.Content = new MemoryStream();
        JsonSerializer.Serialize(message.Content, model, model.GetType());
        message.Content.Position = 0;
    }

    public object Deserialize(HorseMessage message, Type type)
    {
        if (message.Content == null || message.Content.Length < 1)
            return null;

        message.Content.Position = 0;
        return JsonSerializer.Deserialize(message.Content, type);
    }
}

/// <summary>
/// A custom serializer that embeds the runtime type name as a $type discriminator.
/// This simulates Newtonsoft.Json-style polymorphic serialization.
/// </summary>
public class TypeDiscriminatorSerializer : IMessageContentSerializer
{
    public void Serialize(HorseMessage message, object model)
    {
        var wrapper = new Dictionary<string, object>
        {
            ["$type"] = model.GetType().AssemblyQualifiedName,
            ["$value"] = model
        };

        message.Content = new MemoryStream();
        JsonSerializer.Serialize(message.Content, wrapper);
        message.Content.Position = 0;
    }

    public object Deserialize(HorseMessage message, Type type)
    {
        if (message.Content == null || message.Content.Length < 1)
            return null;

        message.Content.Position = 0;
        using var doc = JsonDocument.Parse(message.Content);
        var root = doc.RootElement;

        if (root.TryGetProperty("$type", out var typeProp))
        {
            var actualType = Type.GetType(typeProp.GetString()!);
            if (actualType != null && root.TryGetProperty("$value", out var valueProp))
                return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType);
        }

        message.Content.Position = 0;
        return JsonSerializer.Deserialize(message.Content, type);
    }
}

#endregion

/// <summary>
/// Tests that Push&lt;T&gt; resolves the queue name from the runtime type (model.GetType()),
/// not from the compile-time generic type parameter (typeof(T)). When T is an interface like IEvent,
/// OrderCreatedEvent and PaymentCompletedEvent each go to their own queue based on the concrete type name.
/// </summary>
public class InterfaceTypeResolutionTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithInterfaceType_RoutesToConcreteTypeQueue(string mode)
    {
        // Arrange: create a server with AutoQueueCreation
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        // Producer client
        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        // Consumer subscribes to "OrderCreatedEvent" queue (the concrete type name)
        var concreteConsumer = new HorseClient();
        await concreteConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await concreteConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> concreteMessages = new();
        concreteConsumer.MessageReceived += (_, m) =>
        {
            lock (concreteMessages) concreteMessages.Add(m);
        };

        await Task.Delay(300);

        // Act: Push using interface type parameter - Push<IEvent>(...)
        var order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        var payment = new PaymentCompletedEvent { EventId = "evt-2", Amount = 99.99m };

        // T is IEvent, but runtime type resolution routes to concrete type queues
        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);
        await producer.Queue.Push<IEvent>(payment, true, CancellationToken.None);

        await Task.Delay(1000);

        // Messages should go to concrete type queues, NOT the interface queue
        Assert.NotEmpty(concreteMessages);

        // No "IEvent" queue should have been created
        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        concreteConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithInterfaceType_EachModelGoesToOwnQueue(string mode)
    {
        // Even when T is an interface, each model goes to its own concrete type queue.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        // Subscribe to concrete queue names
        var orderConsumer = new HorseClient();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> orderMessages = new();
        orderConsumer.MessageReceived += (_, m) =>
        {
            lock (orderMessages) orderMessages.Add(m);
        };

        var paymentConsumer = new HorseClient();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentCompletedEvent", true, CancellationToken.None);

        List<HorseMessage> paymentMessages = new();
        paymentConsumer.MessageReceived += (_, m) =>
        {
            lock (paymentMessages) paymentMessages.Add(m);
        };

        await Task.Delay(300);

        // Push both models as IEvent
        var order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        var payment = new PaymentCompletedEvent { EventId = "evt-2", Amount = 99.99m };

        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);
        await producer.Queue.Push<IEvent>(payment, true, CancellationToken.None);

        await Task.Delay(1000);

        // Each model goes to its own concrete type queue
        Assert.Single(orderMessages);
        Assert.Single(paymentMessages);

        // Concrete queues exist, interface queue does NOT
        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.Contains("PaymentCompletedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithConcreteType_RoutesToCorrectQueues(string mode)
    {
        // Control test: When using concrete types, each model goes to its own queue.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        // Subscribe to concrete queue names
        var orderConsumer = new HorseClient();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> orderMessages = new();
        orderConsumer.MessageReceived += (_, m) =>
        {
            lock (orderMessages) orderMessages.Add(m);
        };

        var paymentConsumer = new HorseClient();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentCompletedEvent", true, CancellationToken.None);

        List<HorseMessage> paymentMessages = new();
        paymentConsumer.MessageReceived += (_, m) =>
        {
            lock (paymentMessages) paymentMessages.Add(m);
        };

        await Task.Delay(300);

        // Push with concrete type parameters
        var order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        var payment = new PaymentCompletedEvent { EventId = "evt-2", Amount = 99.99m };

        await producer.Queue.Push(order, true, CancellationToken.None);
        await producer.Queue.Push(payment, true, CancellationToken.None);

        await Task.Delay(1000);

        // Each message goes to its own queue
        Assert.Single(orderMessages);
        Assert.Single(paymentMessages);

        // Both queues exist separately
        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.Contains("PaymentCompletedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushViaQueueBus_WithInterfaceType_RoutesToConcreteTypeQueue(string mode)
    {
        // Same test but through IHorseQueueBus to verify the fix works on the bus API too
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        IHorseQueueBus bus = new HorseQueueBus(client);

        // Subscribe to concrete type queue
        var concreteConsumer = new HorseClient();
        await concreteConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await concreteConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> concreteMessages = new();
        concreteConsumer.MessageReceived += (_, m) =>
        {
            lock (concreteMessages) concreteMessages.Add(m);
        };

        await Task.Delay(300);

        // Push via bus with IEvent as T
        var order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        var headers = new List<KeyValuePair<string, string>> { new("X-Test", "value") };

        await bus.Push<IEvent>(order, true, headers);

        await Task.Delay(1000);

        // Message goes to concrete "OrderCreatedEvent" queue, not "IEvent"
        Assert.NotEmpty(concreteMessages);

        // No "IEvent" queue should have been created
        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        concreteConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_WithInterfaceType_RoutesToConcreteTypeQueue(string mode)
    {
        // PushBulk<IEvent> should route to the concrete type queue based on items[0].GetType()
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        // Subscribe to concrete type queue
        var concreteConsumer = new HorseClient();
        await concreteConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await concreteConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> concreteMessages = new();
        concreteConsumer.MessageReceived += (_, m) =>
        {
            lock (concreteMessages) concreteMessages.Add(m);
        };

        await Task.Delay(300);

        // PushBulk with interface type parameter — NO explicit queue name
        // Runtime type of items[0] is OrderCreatedEvent, so queue name should resolve to "OrderCreatedEvent"
        var items = new List<IEvent>
        {
            new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-1" },
            new OrderCreatedEvent { EventId = "evt-2", OrderNumber = "ORD-2" },
            new OrderCreatedEvent { EventId = "evt-3", OrderNumber = "ORD-3" }
        };

        producer.Queue.PushBulk(items, null);

        for (int i = 0; i < 50 && concreteMessages.Count < 3; i++)
            await Task.Delay(100);

        // All messages should go to concrete "OrderCreatedEvent" queue
        Assert.Equal(3, concreteMessages.Count);

        // No "IEvent" queue should have been created by PushBulk
        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        concreteConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithInterfaceType_QueueNameAttribute_RoutesToAttributeQueue(string mode)
    {
        // When model has [QueueName("OrderQueue")] and we Push<IEvent>(model, ...),
        // message MUST go to "OrderQueue", NOT "IEvent"
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        // Subscribe to the QueueName attribute value
        var orderConsumer = new HorseClient();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> orderMessages = new();
        orderConsumer.MessageReceived += (_, m) =>
        {
            lock (orderMessages) orderMessages.Add(m);
        };

        var paymentConsumer = new HorseClient();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentQueue", true, CancellationToken.None);

        List<HorseMessage> paymentMessages = new();
        paymentConsumer.MessageReceived += (_, m) =>
        {
            lock (paymentMessages) paymentMessages.Add(m);
        };

        await Task.Delay(300);

        // Push as IEvent — runtime type has [QueueName] attribute
        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        IEvent payment = new PaymentCompletedWithQueueNameEvent { EventId = "evt-2", Amount = 99.99m };

        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);
        await producer.Queue.Push<IEvent>(payment, true, CancellationToken.None);

        await Task.Delay(1000);

        // Messages must go to [QueueName] values, NOT "IEvent"
        Assert.Single(orderMessages);
        Assert.Single(paymentMessages);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.Contains("PaymentQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_WithInterfaceType_QueueNameAttribute_RoutesToAttributeQueue(string mode)
    {
        // PushBulk<IEvent> with [QueueName("OrderQueue")] models must route to "OrderQueue"
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        var concreteConsumer = new HorseClient();
        await concreteConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await concreteConsumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> concreteMessages = new();
        concreteConsumer.MessageReceived += (_, m) =>
        {
            lock (concreteMessages) concreteMessages.Add(m);
        };

        await Task.Delay(300);

        var items = new List<IEvent>
        {
            new OrderCreatedWithQueueNameEvent { EventId = "evt-1", OrderNumber = "ORD-1" },
            new OrderCreatedWithQueueNameEvent { EventId = "evt-2", OrderNumber = "ORD-2" },
            new OrderCreatedWithQueueNameEvent { EventId = "evt-3", OrderNumber = "ORD-3" }
        };

        producer.Queue.PushBulk(items, null);

        for (int i = 0; i < 50 && concreteMessages.Count < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, concreteMessages.Count);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        concreteConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushViaBus_WithInterfaceType_QueueNameAttribute_RoutesToAttributeQueue(string mode)
    {
        // IHorseQueueBus.Push<IEvent>(model, waitForCommit, headers) must route to [QueueName] value
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        IHorseQueueBus bus = new HorseQueueBus(client);

        var concreteConsumer = new HorseClient();
        await concreteConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await concreteConsumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> concreteMessages = new();
        concreteConsumer.MessageReceived += (_, m) =>
        {
            lock (concreteMessages) concreteMessages.Add(m);
        };

        await Task.Delay(300);

        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        var headers = new List<KeyValuePair<string, string>> { new("X-Test", "value") };

        await bus.Push<IEvent>(order, true, headers);

        await Task.Delay(1000);

        Assert.Single(concreteMessages);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        concreteConsumer.Disconnect();
    }

    #region Generic Publisher Wrapper Tests

    /// <summary>
    /// Simulates a real-world generic publisher that constrains TEvent to IEvent.
    /// When the caller does publisher.RaiseEvent(new OrderCreatedEvent()),
    /// TEvent is inferred as OrderCreatedEvent at compile-time but may also
    /// be explicitly called as publisher.RaiseEvent&lt;IEvent&gt;(order).
    /// </summary>
    private class TestPublisher
    {
        private readonly QueueOperator _queue;

        public TestPublisher(QueueOperator queue) => _queue = queue;

        public Task<HorseResult> RaiseEvent<TEvent>(TEvent ev, CancellationToken cancellationToken) where TEvent : class, IEvent
        {
            return _queue.Push(ev, true, cancellationToken);
        }

        public Task<HorseResult> RaiseEventWithHeaders<TEvent>(TEvent ev,
            IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken) where TEvent : class, IEvent
        {
            return _queue.Push(ev, true, headers, cancellationToken);
        }
    }

    private class TestBusPublisher
    {
        private readonly IHorseQueueBus _bus;

        public TestBusPublisher(IHorseQueueBus bus) => _bus = bus;

        public Task<HorseResult> RaiseEvent<TEvent>(TEvent ev, CancellationToken cancellationToken) where TEvent : class, IEvent
        {
            return _bus.Push(ev, true, cancellationToken);
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task GenericPublisher_InferredConcreteType_RoutesToConcreteQueue(string mode)
    {
        // publisher.RaiseEvent(new OrderCreatedEvent()) — TEvent inferred as OrderCreatedEvent
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        var publisher = new TestPublisher(client.Queue);

        // TEvent is inferred as OrderCreatedEvent at compile-time
        await publisher.RaiseEvent(new OrderCreatedEvent { EventId = "e1", OrderNumber = "ORD-1" }, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task GenericPublisher_ExplicitInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // publisher.RaiseEvent<IEvent>(new OrderCreatedEvent()) — TEvent explicitly IEvent
        // This is the real-world scenario: developer has IEvent variable and calls generic method
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        var orderConsumer = new HorseClient();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> orderMsgs = new();
        orderConsumer.MessageReceived += (_, m) => { lock (orderMsgs) orderMsgs.Add(m); };

        var paymentConsumer = new HorseClient();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentCompletedEvent", true, CancellationToken.None);

        List<HorseMessage> paymentMsgs = new();
        paymentConsumer.MessageReceived += (_, m) => { lock (paymentMsgs) paymentMsgs.Add(m); };

        await Task.Delay(300);

        var publisher = new TestPublisher(client.Queue);

        // Explicitly specifying IEvent as the type parameter — simulates real-world usage
        IEvent order = new OrderCreatedEvent { EventId = "e1", OrderNumber = "ORD-1" };
        IEvent payment = new PaymentCompletedEvent { EventId = "e2", Amount = 50.0m };

        await publisher.RaiseEvent<IEvent>(order, CancellationToken.None);
        await publisher.RaiseEvent<IEvent>(payment, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(orderMsgs);
        Assert.Single(paymentMsgs);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.Contains("PaymentCompletedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task GenericPublisher_ExplicitInterfaceType_WithQueueNameAttr_RoutesToAttributeQueue(string mode)
    {
        // publisher.RaiseEvent<IEvent>(new OrderCreatedWithQueueNameEvent())
        // Model has [QueueName("OrderQueue")] — must route there, not "IEvent"
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        var orderConsumer = new HorseClient();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> orderMsgs = new();
        orderConsumer.MessageReceived += (_, m) => { lock (orderMsgs) orderMsgs.Add(m); };

        var paymentConsumer = new HorseClient();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentQueue", true, CancellationToken.None);

        List<HorseMessage> paymentMsgs = new();
        paymentConsumer.MessageReceived += (_, m) => { lock (paymentMsgs) paymentMsgs.Add(m); };

        await Task.Delay(300);

        var publisher = new TestPublisher(client.Queue);

        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "e1", OrderNumber = "ORD-1" };
        IEvent payment = new PaymentCompletedWithQueueNameEvent { EventId = "e2", Amount = 75.0m };

        await publisher.RaiseEvent<IEvent>(order, CancellationToken.None);
        await publisher.RaiseEvent<IEvent>(payment, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(orderMsgs);
        Assert.Single(paymentMsgs);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.Contains("PaymentQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);
        Assert.DoesNotContain("OrderCreatedWithQueueNameEvent", allQueues);
        Assert.DoesNotContain("PaymentCompletedWithQueueNameEvent", allQueues);

        client.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task GenericBusPublisher_ExplicitInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // Same scenario through IHorseQueueBus instead of QueueOperator directly
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        IHorseQueueBus bus = new HorseQueueBus(client);
        var busPublisher = new TestBusPublisher(bus);

        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "e1", OrderNumber = "ORD-1" };
        await busPublisher.RaiseEvent<IEvent>(order, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        consumer.Disconnect();
    }

    #endregion

    #region Custom Serializer Tests

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_NaiveSerializer_PushWithInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // A custom serializer should not affect queue routing.
        // Queue name is resolved BEFORE serialization happens.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        producer.MessageSerializer = new NaiveCustomSerializer();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        var consumer = new HorseClient();
        consumer.MessageSerializer = new NaiveCustomSerializer();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        IEvent order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_NaiveSerializer_QueueNameAttr_RoutesToAttributeQueue(string mode)
    {
        // Custom serializer + [QueueName("OrderQueue")] + Push<IEvent> → must go to "OrderQueue"
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        producer.MessageSerializer = new NaiveCustomSerializer();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        var consumer = new HorseClient();
        consumer.MessageSerializer = new NaiveCustomSerializer();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);
        Assert.DoesNotContain("OrderCreatedWithQueueNameEvent", allQueues);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_TypeDiscriminator_PushWithInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // TypeDiscriminatorSerializer wraps the payload with $type/$value.
        // Queue routing must still use model.GetType(), not anything from serialization.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        producer.MessageSerializer = new TypeDiscriminatorSerializer();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        var orderConsumer = new HorseClient();
        orderConsumer.MessageSerializer = new TypeDiscriminatorSerializer();
        await orderConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await orderConsumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        var paymentConsumer = new HorseClient();
        paymentConsumer.MessageSerializer = new TypeDiscriminatorSerializer();
        await paymentConsumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await paymentConsumer.Queue.Subscribe("PaymentCompletedEvent", true, CancellationToken.None);

        List<HorseMessage> orderMsgs = new();
        orderConsumer.MessageReceived += (_, m) => { lock (orderMsgs) orderMsgs.Add(m); };

        List<HorseMessage> paymentMsgs = new();
        paymentConsumer.MessageReceived += (_, m) => { lock (paymentMsgs) paymentMsgs.Add(m); };

        await Task.Delay(300);

        IEvent order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        IEvent payment = new PaymentCompletedEvent { EventId = "evt-2", Amount = 49.99m };

        await producer.Queue.Push<IEvent>(order, true, CancellationToken.None);
        await producer.Queue.Push<IEvent>(payment, true, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(orderMsgs);
        Assert.Single(paymentMsgs);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.Contains("PaymentCompletedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        orderConsumer.Disconnect();
        paymentConsumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_GenericPublisher_ExplicitInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // Full real-world scenario: custom serializer + generic publisher wrapper + interface type
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        client.MessageSerializer = new NaiveCustomSerializer();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        var consumer = new HorseClient();
        consumer.MessageSerializer = new NaiveCustomSerializer();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderQueue", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        var publisher = new TestPublisher(client.Queue);

        IEvent order = new OrderCreatedWithQueueNameEvent { EventId = "e1", OrderNumber = "ORD-1" };
        await publisher.RaiseEvent<IEvent>(order, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderQueue", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_BusPublisher_ExplicitInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // IHorseQueueBus + custom serializer + Push<IEvent> → must route to concrete queue
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var client = new HorseClient();
        client.MessageSerializer = new NaiveCustomSerializer();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        IHorseQueueBus bus = new HorseQueueBus(client);

        var consumer = new HorseClient();
        consumer.MessageSerializer = new NaiveCustomSerializer();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        IEvent order = new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
        await bus.Push<IEvent>(order, true, CancellationToken.None);

        await Task.Delay(1000);

        Assert.Single(received);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        client.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task CustomSerializer_PushBulk_WithInterfaceType_RoutesToConcreteQueue(string mode)
    {
        // PushBulk<IEvent> + custom serializer → must route to concrete queue
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.AutoQueueCreation = true;
        });

        var producer = new HorseClient();
        producer.MessageSerializer = new NaiveCustomSerializer();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(producer.IsConnected);

        var consumer = new HorseClient();
        consumer.MessageSerializer = new NaiveCustomSerializer();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("OrderCreatedEvent", true, CancellationToken.None);

        List<HorseMessage> received = new();
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m); };

        await Task.Delay(300);

        var items = new List<IEvent>
        {
            new OrderCreatedEvent { EventId = "evt-1", OrderNumber = "ORD-1" },
            new OrderCreatedEvent { EventId = "evt-2", OrderNumber = "ORD-2" }
        };

        producer.Queue.PushBulk(items, null);

        for (int i = 0; i < 50 && received.Count < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, received.Count);

        var allQueues = ctx.Rider.Queue.Queues.Select(q => q.Name).ToList();
        Assert.Contains("OrderCreatedEvent", allQueues);
        Assert.DoesNotContain("IEvent", allQueues);

        producer.Disconnect();
        consumer.Disconnect();
    }

    #endregion
}
