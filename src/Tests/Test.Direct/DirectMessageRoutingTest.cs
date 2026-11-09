using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;
using Test.Common;
using Xunit;

namespace Test.Direct;

/// <summary>
/// Routing, response and authorization rules of direct messages (by id, @name: and @type:)
/// </summary>
public class DirectMessageRoutingTest
{
    //HorseLogEvents.ClientHandler and HorseLogEvents.DirectMessageHandler (internal)
    private const int ClientHandlerEvent = 1101;
    private const int DirectMessageHandlerEvent = 1102;

    private sealed class RecordingDirectHandler : IDirectMessageHandler
    {
        public int Direct;
        public int NotFound;
        public int Response;
        public List<MessagingClient> LastReceivers;

        public Task OnDirect(MessagingClient sender, HorseMessage message, List<MessagingClient> receivers)
        {
            LastReceivers = receivers;
            Interlocked.Increment(ref Direct);
            return Task.CompletedTask;
        }

        public Task OnNotFound(MessagingClient sender, HorseMessage message)
        {
            Interlocked.Increment(ref NotFound);
            return Task.CompletedTask;
        }

        public Task OnResponse(MessagingClient sender, HorseMessage message, MessagingClient receiver)
        {
            Interlocked.Increment(ref Response);
            return Task.CompletedTask;
        }
    }

    private sealed class DirectAuthorization : IClientAuthorization
    {
        public Func<MessagingClient, MessagingClient, bool> CanSend { get; set; } = (_, _) => true;

        public Task<bool> CanDirectMessage(MessagingClient sender, HorseMessage message, MessagingClient receiver) => Task.FromResult(CanSend(sender, receiver));
        public Task<bool> CanCreateQueue(MessagingClient client, string name, NetworkOptionsBuilder options) => Task.FromResult(true);
        public Task<bool> CanMessageToQueue(MessagingClient client, HorseQueue queue, HorseMessage message) => Task.FromResult(true);
        public Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue) => Task.FromResult(true);
        public bool CanSubscribeEvent(MessagingClient client, HorseEventType eventType, string target) => true;
        public Task<bool> CanCreateRouter(MessagingClient client, string routerName, RouteMethod method) => Task.FromResult(true);
        public Task<bool> CanRemoveRouter(MessagingClient client, Router router) => Task.FromResult(true);
        public Task<bool> CanCreateBinding(MessagingClient client, Router router, BindingInformation binding) => Task.FromResult(true);
        public Task<bool> CanRemoveBinding(MessagingClient client, Binding binding) => Task.FromResult(true);
    }

    private sealed class ThrowingDirectHandler : IDirectMessageHandler
    {
        //synchronous throw
        public Task OnDirect(MessagingClient sender, HorseMessage message, List<MessagingClient> receivers) => throw new InvalidOperationException("direct");

        //task that fails later
        public async Task OnNotFound(MessagingClient sender, HorseMessage message)
        {
            await Task.Yield();
            throw new InvalidOperationException("not found");
        }

        public Task OnResponse(MessagingClient sender, HorseMessage message, MessagingClient receiver) => throw new InvalidOperationException("response");
    }

    private sealed class ThrowingClientHandler : IClientHandler
    {
        public Task Connected(HorseRider server, MessagingClient client) => throw new InvalidOperationException("connected");

        public async Task Disconnected(HorseRider server, MessagingClient client)
        {
            await Task.Yield();
            throw new InvalidOperationException("disconnected");
        }
    }

    private sealed class RecordingErrorHandler : IErrorHandler
    {
        public ConcurrentQueue<(int EventId, string Message)> Errors { get; } = new();

        public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
        {
            Errors.Enqueue((eventId, message));
        }

        public bool Has(int eventId, string handlerName) => Errors.Any(x => x.EventId == eventId && x.Message.StartsWith(handlerName));
    }

    private static async Task<HorseClient> Connect(int port, string id, string name = null, string type = null)
    {
        HorseClient client = new HorseClient();
        client.ClientId = id;
        client.ResponseTimeout = TimeSpan.FromSeconds(3);

        if (name != null)
            client.SetClientName(name);

        if (type != null)
            client.SetClientType(type);

        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static HorseMessage Direct(string target)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target);
        message.SetStringContent("hello");
        return message;
    }

    private static Func<int> CountDirectMessages(HorseClient client)
    {
        int count = 0;
        client.MessageReceived += (_, m) =>
        {
            if (m.Type == MessageType.DirectMessage)
                Interlocked.Increment(ref count);
        };

        return () => Volatile.Read(ref count);
    }

    private static Func<int> CountResponses(HorseClient client)
    {
        int count = 0;
        client.CatchResponseMessages = true;
        client.MessageReceived += (_, m) =>
        {
            if (m.Type == MessageType.Response)
                Interlocked.Increment(ref count);
        };

        return () => Volatile.Read(ref count);
    }

    [Theory]
    [InlineData("@name:nobody")]
    [InlineData("@type:nobody")]
    public async Task UnknownNameOrType_DoesNotDisconnectSender(string target)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = await Connect(port, "sender");
            int disconnects = 0;
            sender.Disconnected += _ => Interlocked.Increment(ref disconnects);

            await sender.SendAsync(Direct(target), CancellationToken.None);
            HorseResult result = await sender.SendAsync(Direct(target), true, CancellationToken.None);

            Assert.Equal(HorseResultCode.NotFound, result.Code);
            await Task.Delay(300);
            Assert.Equal(0, disconnects);
            Assert.True(sender.IsConnected);
        });
    }

    [Fact]
    public async Task UniqueIdNameAndType_AreTrimmed_AndLookupsIgnoreWhiteSpace()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient victim = await Connect(port, "victim", " svc ", " worker ");
            Func<int> victimReceived = CountDirectMessages(victim);

            MessagingClient registered = server.Rider.Client.Find("victim");
            Assert.NotNull(registered);
            Assert.Equal("svc", registered.Name);
            Assert.Equal("worker", registered.Type);

            //same id with white space is the same client, it cannot register next to it
            HorseClient impostor = new HorseClient();
            impostor.ClientId = "victim ";
            impostor.SetClientName("impostor");
            await impostor.ConnectAsync("horse://localhost:" + port);
            await Task.Delay(300);

            Assert.Same(registered, server.Rider.Client.Find("victim"));
            Assert.Equal(1, server.Rider.Client.GetOnlineClients());
            impostor.Disconnect();

            HorseClient sender = await Connect(port, "sender");
            await sender.SendAsync(Direct("victim "), CancellationToken.None);
            await sender.SendAsync(Direct("@name: svc "), CancellationToken.None);
            await sender.SendAsync(Direct("@type:worker "), CancellationToken.None);

            await TestHorseRider.WaitUntil(() => victimReceived() == 3);
            Assert.Equal(3, victimReceived());
        });
    }

    [Fact]
    public async Task MissingReceiver_RespondsNotFoundOnlyWhenSenderWaits()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingDirectHandler handler = new RecordingDirectHandler();
            server.Rider.Direct.MessageHandlers.Add(handler);

            HorseClient sender = await Connect(port, "sender");
            Func<int> responses = CountResponses(sender);

            await sender.SendAsync(Direct("missing-id"), CancellationToken.None);
            await Task.Delay(300);
            Assert.Equal(0, responses());

            HorseResult result = await sender.SendAsync(Direct("missing-id"), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.NotFound, result.Code);
            await TestHorseRider.WaitUntil(() => handler.NotFound == 2);
            Assert.Equal(2, handler.NotFound);
        });
    }

    [Fact]
    public async Task AllReceiversDenied_RespondsUnauthorizedOnce_OnlyWhenSenderWaits()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Client.Authorizations.Add(new DirectAuthorization { CanSend = (_, _) => false });

            HorseClient sender = await Connect(port, "sender");
            Func<int> responses = CountResponses(sender);
            await Connect(port, "r1", type: "t");
            await Connect(port, "r2", type: "t");
            await Connect(port, "r3", type: "t");

            await sender.SendAsync(Direct("@type:t"), CancellationToken.None);
            await sender.SendAsync(Direct("r1"), CancellationToken.None);
            await Task.Delay(300);
            Assert.Equal(0, responses());

            HorseResult result = await sender.SendAsync(Direct("@type:t"), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Unauthorized, result.Code);
            await Task.Delay(300);
            Assert.Equal(1, responses());
        });
    }

    [Fact]
    public async Task PartiallyDenied_SendsToAllowedOnly_AndHandlerGetsDeliveredReceivers()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingDirectHandler handler = new RecordingDirectHandler();
            server.Rider.Direct.MessageHandlers.Add(handler);
            server.Rider.Client.Authorizations.Add(new DirectAuthorization { CanSend = (_, receiver) => receiver.Name != "denied" });

            HorseClient sender = await Connect(port, "sender");
            Func<int> responses = CountResponses(sender);
            Func<int> r1 = CountDirectMessages(await Connect(port, "r1", "allowed", "t"));
            Func<int> r2 = CountDirectMessages(await Connect(port, "r2", "denied", "t"));
            Func<int> r3 = CountDirectMessages(await Connect(port, "r3", "allowed", "t"));

            await sender.SendAsync(Direct("@type:t"), CancellationToken.None);
            await TestHorseRider.WaitUntil(() => handler.Direct == 1 && r1() == 1 && r3() == 1);
            await Task.Delay(200);

            Assert.Equal(1, r1());
            Assert.Equal(0, r2());
            Assert.Equal(1, r3());
            Assert.Equal(0, responses());
            Assert.Equal(new[] { "r1", "r3" }, handler.LastReceivers.Select(x => x.UniqueId).ToArray());
        });
    }

    [Fact]
    public async Task AcknowledgedTypeMessage_GoesToAllReceivers_HighPriorityToFirstOnly()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = await Connect(port, "sender");
            HorseClient c1 = await Connect(port, "r1", type: "t");
            HorseClient c2 = await Connect(port, "r2", type: "t");
            HorseClient c3 = await Connect(port, "r3", type: "t");
            c1.AutoAcknowledge = c2.AutoAcknowledge = c3.AutoAcknowledge = true;
            Func<int> r1 = CountDirectMessages(c1);
            Func<int> r2 = CountDirectMessages(c2);
            Func<int> r3 = CountDirectMessages(c3);

            HorseResult result = await sender.SendAsync(Direct("@type:t"), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await TestHorseRider.WaitUntil(() => r1() == 1 && r2() == 1 && r3() == 1);
            Assert.Equal((1, 1, 1), (r1(), r2(), r3()));

            for (int i = 0; i < 5; i++)
            {
                HorseMessage message = Direct("@type:t");
                message.HighPriority = true;
                result = await sender.SendAsync(message, true, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, result.Code);
            }

            await Task.Delay(300);
            Assert.Equal((6, 1, 1), (r1(), r2(), r3()));
        });
    }

    [Fact]
    public async Task Response_TriggersOnResponse()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingDirectHandler handler = new RecordingDirectHandler();
            server.Rider.Direct.MessageHandlers.Add(handler);

            HorseClient requester = await Connect(port, "requester");
            HorseClient responder = await Connect(port, "responder");
            responder.MessageReceived += async (_, m) =>
            {
                if (m.Type == MessageType.DirectMessage && m.WaitResponse)
                    await responder.SendAsync(m.CreateResponse(HorseResultCode.Ok), CancellationToken.None);
            };

            HorseMessage response = await requester.Request(Direct("responder"), CancellationToken.None);
            Assert.NotNull(response);

            await TestHorseRider.WaitUntil(() => handler.Response == 1);
            Assert.Equal(1, handler.Direct);
            Assert.Equal(1, handler.Response);
        });
    }

    [Fact]
    public async Task EnvelopedMessage_SourceIsTheSender()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient attacker = await Connect(port, "attacker");
            HorseClient target = await Connect(port, "target");
            TaskCompletionSource<string> source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            target.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.DirectMessage)
                    source.TrySetResult(m.Source);
            };

            HorseMessage inner = Direct("target");
            inner.SetSource("admin-service");
            inner.SetMessageId("inner-1");

            HorseMessage envelope = new HorseMessage(MessageType.Envelope, "*", 0);
            envelope.SetMessageId("envelope-1");
            envelope.Content = new MemoryStream();
            envelope.Content.Write(HorseProtocolWriter.Create(inner));
            envelope.CalculateLengths();
            await attacker.SendAsync(envelope, CancellationToken.None);

            Task completed = await Task.WhenAny(source.Task, Task.Delay(3000));
            Assert.Same(source.Task, completed);
            Assert.Equal("attacker", source.Task.Result);
        });
    }

    [Fact]
    public async Task Stats_CountDirectMessagesAndResponses()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient requester = await Connect(port, "requester");
            HorseClient responder = await Connect(port, "responder", type: "t");
            responder.MessageReceived += async (_, m) =>
            {
                if (m.Type == MessageType.DirectMessage && m.WaitResponse)
                    await responder.SendAsync(m.CreateResponse(HorseResultCode.Ok), CancellationToken.None);
            };

            await requester.SendAsync(Direct("responder"), CancellationToken.None);
            await requester.SendAsync(Direct("@type:t"), CancellationToken.None);
            HorseMessage response = await requester.Request(Direct("responder"), CancellationToken.None);
            Assert.NotNull(response);

            MessagingClient requesterOnServer = server.Rider.Client.Find("requester");
            MessagingClient responderOnServer = server.Rider.Client.Find("responder");
            await TestHorseRider.WaitUntil(() => requesterOnServer.Stats.ReceivedDirectMessages == 1);

            Assert.Equal(3, requesterOnServer.Stats.SentDirectMessages);
            Assert.Equal(3, responderOnServer.Stats.ReceivedDirectMessages);
            Assert.Equal(1, responderOnServer.Stats.SentDirectMessages);
            Assert.Equal(1, requesterOnServer.Stats.ReceivedDirectMessages);
        });
    }

    [Fact]
    public async Task FailingHandlers_AreReported_AndDoNotAffectClients()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingErrorHandler errors = new RecordingErrorHandler();
            server.Rider.ErrorHandlers.Add(errors);
            server.Rider.Direct.MessageHandlers.Add(new ThrowingDirectHandler());
            server.Rider.Client.Handlers.Add(new ThrowingClientHandler());

            HorseClient sender = await Connect(port, "sender");
            HorseClient receiver = await Connect(port, "receiver");
            Func<int> received = CountDirectMessages(receiver);
            int disconnects = 0;
            sender.Disconnected += _ => Interlocked.Increment(ref disconnects);
            receiver.MessageReceived += async (_, m) =>
            {
                if (m.Type == MessageType.DirectMessage && m.WaitResponse)
                    await receiver.SendAsync(m.CreateResponse(HorseResultCode.Ok), CancellationToken.None);
            };

            await sender.SendAsync(Direct("receiver"), CancellationToken.None);
            await sender.SendAsync(Direct("missing"), CancellationToken.None);
            HorseMessage response = await sender.Request(Direct("receiver"), CancellationToken.None);
            Assert.NotNull(response);
            receiver.Disconnect();

            await TestHorseRider.WaitUntil(() => errors.Has(ClientHandlerEvent, "IClientHandler.Disconnected"));

            Assert.Equal(2, received());
            Assert.Equal(0, disconnects);
            Assert.True(sender.IsConnected);
            Assert.True(errors.Has(DirectMessageHandlerEvent, "IDirectMessageHandler.OnDirect"));
            Assert.True(errors.Has(DirectMessageHandlerEvent, "IDirectMessageHandler.OnNotFound"));
            Assert.True(errors.Has(DirectMessageHandlerEvent, "IDirectMessageHandler.OnResponse"));
            Assert.True(errors.Has(ClientHandlerEvent, "IClientHandler.Connected"));
            Assert.True(errors.Has(ClientHandlerEvent, "IClientHandler.Disconnected"));
        });
    }

    [Fact]
    public async Task OnlineCount_AndClients_FollowConnectAndDisconnect()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient c1 = await Connect(port, "c1");
            await Connect(port, "c2");
            await Connect(port, "c3");
            Assert.Equal(3, server.Rider.Client.GetOnlineClients());

            c1.Disconnect();
            await TestHorseRider.WaitUntil(() => server.Rider.Client.GetOnlineClients() == 2);

            Assert.Equal(2, server.Rider.Client.GetOnlineClients());
            Assert.Equal(new[] { "c2", "c3" }, server.Rider.Client.Clients.Select(x => x.UniqueId).OrderBy(x => x).ToArray());
        });
    }

    [Fact]
    public async Task NameAndTypeBuckets_KeepConnectionOrder_AndEmptyBucketsAreRemoved()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient c1 = await Connect(port, "c1", "unique-name", "t");
            await Connect(port, "c2", type: "t");
            await Connect(port, "c3", type: "t");

            c1.Disconnect();
            await TestHorseRider.WaitUntil(() => server.Rider.Client.Find("c1") == null);
            await Connect(port, "c4", type: "t");

            Assert.Equal(new[] { "c2", "c3", "c4" }, server.Rider.Client.FindByType("t").Select(x => x.UniqueId).ToArray());

            MessagingClient[] byName = server.Rider.Client.FindClientByName("unique-name");
            Assert.NotNull(byName);
            Assert.Empty(byName);

            FieldInfo field = typeof(ClientRider).GetField("_clientsByName", BindingFlags.NonPublic | BindingFlags.Instance);
            System.Collections.IDictionary buckets = (System.Collections.IDictionary)field!.GetValue(server.Rider.Client);
            Assert.False(buckets!.Contains("unique-name"));
        });
    }
}
