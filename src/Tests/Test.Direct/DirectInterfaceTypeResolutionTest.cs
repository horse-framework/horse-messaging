using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Direct;

#region Test Models

public interface IDirectEvent
{
    string EventId { get; set; }
}

[DirectTarget(FindTargetBy.Id, "client-receiver")]
[DirectContentType(1001)]
public class DirectOrderEvent : IDirectEvent
{
    public string EventId { get; set; }
    public string OrderNumber { get; set; }
}

[DirectTarget(FindTargetBy.Id, "client-receiver")]
[DirectContentType(1002)]
public class DirectPaymentEvent : IDirectEvent
{
    public string EventId { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// A model with no DirectTarget attribute — used to verify that Send fails gracefully
/// when the interface has no routing attributes.
/// </summary>
public class UnattributedDirectEvent : IDirectEvent
{
    public string EventId { get; set; }
}

#endregion

/// <summary>
/// Tests that DirectOperator.Send&lt;T&gt; resolves the target from the runtime type (model.GetType()),
/// not from the compile-time generic type parameter (typeof(T)).
/// When T is an interface like IDirectEvent, the concrete type's [DirectTarget] and [DirectContentType]
/// attributes must be used for routing.
/// </summary>
public class DirectInterfaceTypeResolutionTest
{
    [Fact]
    public async Task Send_WithInterfaceType_ResolvesConcreteTypeAttributes()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            HorseClient receiver = new HorseClient();
            receiver.ClientId = "client-receiver";
            receiver.AutoAcknowledge = true;

            await sender.ConnectAsync($"horse://localhost:{port}");
            await receiver.ConnectAsync($"horse://localhost:{port}");

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            List<HorseMessage> receivedMessages = new();
            receiver.MessageReceived += (_, m) =>
            {
                lock (receivedMessages) receivedMessages.Add(m);
            };

            // Act: Send<IDirectEvent> with a concrete DirectOrderEvent
            // T = IDirectEvent, but runtime type = DirectOrderEvent which has [DirectTarget] and [DirectContentType(1001)]
            var order = new DirectOrderEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
            HorseResult result = await sender.Direct.Send<IDirectEvent>(order, true, CancellationToken.None);

            await Task.Delay(1000);

            // Assert: Message should arrive at receiver with correct content type from concrete class
            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.NotEmpty(receivedMessages);
            Assert.Equal(1001, receivedMessages[0].ContentType);
        });
    }

    [Fact]
    public async Task Send_WithInterfaceType_DifferentConcreteTypes_DifferentContentTypes()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            HorseClient receiver = new HorseClient();
            receiver.ClientId = "client-receiver";
            receiver.AutoAcknowledge = true;

            await sender.ConnectAsync($"horse://localhost:{port}");
            await receiver.ConnectAsync($"horse://localhost:{port}");

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            List<HorseMessage> receivedMessages = new();
            receiver.MessageReceived += (_, m) =>
            {
                lock (receivedMessages) receivedMessages.Add(m);
            };

            // Act: Send two different concrete types via the interface generic parameter
            var order = new DirectOrderEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
            var payment = new DirectPaymentEvent { EventId = "evt-2", Amount = 99.99m };

            HorseResult r1 = await sender.Direct.Send<IDirectEvent>(order, true, CancellationToken.None);
            HorseResult r2 = await sender.Direct.Send<IDirectEvent>(payment, true, CancellationToken.None);

            await Task.Delay(1000);

            // Assert: Both messages arrive, each with its own concrete ContentType
            Assert.Equal(HorseResultCode.Ok, r1.Code);
            Assert.Equal(HorseResultCode.Ok, r2.Code);
            Assert.Equal(2, receivedMessages.Count);

            // Messages should have different content types based on concrete class attributes
            var contentTypes = new HashSet<ushort>(receivedMessages.ConvertAll(m => m.ContentType));
            Assert.Contains((ushort)1001, contentTypes);
            Assert.Contains((ushort)1002, contentTypes);
        });
    }

    [Fact]
    public async Task Send_WithConcreteType_ResolvesCorrectly()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            HorseClient receiver = new HorseClient();
            receiver.ClientId = "client-receiver";
            receiver.AutoAcknowledge = true;

            await sender.ConnectAsync($"horse://localhost:{port}");
            await receiver.ConnectAsync($"horse://localhost:{port}");

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            HorseMessage receivedMessage = null;
            receiver.MessageReceived += (_, m) => receivedMessage = m;

            var order = new DirectOrderEvent { EventId = "evt-1", OrderNumber = "ORD-100" };
            HorseResult result = await sender.Direct.Send(order, true, CancellationToken.None);

            await Task.Delay(1000);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.NotNull(receivedMessage);
            Assert.Equal(1001, receivedMessage.ContentType);
        });
    }

    [Fact]
    public async Task Send_WithInterfaceType_NoAttributeOnInterface_UsesConcreteAttributes()
    {
        // IDirectEvent has no [DirectTarget] attribute.
        // When Send<IDirectEvent>(concreteModel) is called, the concrete type's attributes must be used.
        // If IDirectEvent was used (the old bug), it would fail because IDirectEvent has no target.
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            HorseClient receiver = new HorseClient();
            receiver.ClientId = "client-receiver";
            receiver.AutoAcknowledge = true;

            await sender.ConnectAsync($"horse://localhost:{port}");
            await receiver.ConnectAsync($"horse://localhost:{port}");

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            HorseMessage receivedMessage = null;
            receiver.MessageReceived += (_, m) => receivedMessage = m;

            // This would have returned SendError with the old typeof(T) bug
            // because IDirectEvent interface has no [DirectTarget] attribute
            var order = new DirectOrderEvent { EventId = "evt-1", OrderNumber = "ORD-200" };
            HorseResult result = await sender.Direct.Send<IDirectEvent>(order, true, CancellationToken.None);

            await Task.Delay(1000);

            // Should succeed because runtime type DirectOrderEvent has [DirectTarget]
            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.NotNull(receivedMessage);
        });
    }

    [Fact]
    public async Task Send_UnattributedConcreteType_ReturnsSendError()
    {
        // When the concrete type itself has no [DirectTarget], Send returns SendError

        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            await sender.ConnectAsync($"horse://localhost:{port}");
            Assert.True(sender.IsConnected);

            var model = new UnattributedDirectEvent { EventId = "evt-1" };
            HorseResult result = await sender.Direct.Send(model, false, CancellationToken.None);

            Assert.Equal(HorseResultCode.SendError, result.Code);
        });
    }

    [Fact]
    public async Task Send_WithInterfaceType_UnattributedConcrete_ReturnsSendError()
    {
        // Same scenario via interface: Send<IDirectEvent>(unattributedModel) should also return SendError

        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = new HorseClient();
            sender.ClientId = "client-sender";

            await sender.ConnectAsync($"horse://localhost:{port}");
            Assert.True(sender.IsConnected);

            var model = new UnattributedDirectEvent { EventId = "evt-1" };
            HorseResult result = await sender.Direct.Send<IDirectEvent>(model, false, CancellationToken.None);

            Assert.Equal(HorseResultCode.SendError, result.Code);
        });
    }
}