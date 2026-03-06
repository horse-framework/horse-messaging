using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
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
}

