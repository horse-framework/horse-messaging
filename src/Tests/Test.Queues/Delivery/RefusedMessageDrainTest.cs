using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Xunit;

namespace Test.Queues.Delivery;

/// <summary>
/// A push queue message that every consumer refuses (CanConsumerReceive) stays in the queue.
/// It must not stop the drain: the messages behind it, and the regular store behind a refused
/// priority message, are still delivered, and a pass whose messages are all refused ends.
/// </summary>
public class RefusedMessageDrainTest
{
    [Fact]
    public async Task RefusedPriorityMessage_DoesNotStarveRegularMessages()
    {
        await using var ctx = await Context.Create(QueueAckDecision.None);
        ctx.Handler.Refuse = m => m.Message.MessageId == "p";
        ctx.Enqueue("p", priority: true);
        ctx.Enqueue("r1");
        ctx.Enqueue("r2");

        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["r1", "r2"], ctx.Client.DeliveredIds);
        Assert.Equal("p", ctx.Manager.PriorityMessageStore.ReadFirst().Message.MessageId);
        Assert.Equal(0, ctx.Manager.MessageStore.Count());
        Assert.Equal(1, ctx.Handler.Offers("p"));
    }

    [Fact]
    public async Task RefusedMessage_DoesNotBlockTheMessagesBehindIt()
    {
        await using var ctx = await Context.Create(QueueAckDecision.None);
        ctx.Handler.Refuse = m => m.Message.MessageId == "f";
        ctx.Enqueue("f");
        ctx.Enqueue("a");
        ctx.Enqueue("b");

        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["a", "b"], ctx.Client.DeliveredIds);
        Assert.Equal("f", ctx.Manager.MessageStore.ReadFirst().Message.MessageId);
        Assert.Equal(1, ctx.Handler.Offers("f"));
    }

    [Fact]
    public async Task AllMessagesRefused_PassOffersEachOnceAndEnds()
    {
        await using var ctx = await Context.Create(QueueAckDecision.None);
        ctx.Handler.Refuse = _ => true;
        ctx.Enqueue("p1", priority: true);
        ctx.Enqueue("f1");
        ctx.Enqueue("f2");

        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(ctx.Client.DeliveredIds);
        Assert.Equal(3, ctx.Handler.TotalOffers);
        Assert.Equal(1, ctx.Manager.PriorityMessageStore.Count());
        Assert.Equal(2, ctx.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task RefusedMessage_IsOfferedAgainInTheNextPass()
    {
        await using var ctx = await Context.Create(QueueAckDecision.None);
        ctx.Handler.Refuse = _ => true;
        ctx.Enqueue("later");

        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(ctx.Client.DeliveredIds);

        ctx.Handler.Refuse = _ => false;
        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["later"], ctx.Client.DeliveredIds);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Fact]
    public async Task BlockedConsumer_StillEndsThePassAtTheFirstMessage()
    {
        // Unavailable consumers are not about the message: offering every other message would
        // only repeat the same failure for the whole queue.
        await using var ctx = await Context.Create(QueueAckDecision.None);
        ctx.Subscription.Blocked = true;
        ctx.Enqueue("m1");
        ctx.Enqueue("m2");
        ctx.Enqueue("m3");

        await ctx.Queue.Trigger().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(ctx.Client.DeliveredIds);
        Assert.Equal(1, ctx.Handler.TotalOffers);
        Assert.Equal(3, ctx.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task WaitForAcknowledge_RefusedMessage_DoesNotHoldTheAcknowledgeLock()
    {
        await using var ctx = await Context.Create(QueueAckDecision.WaitForAcknowledge);
        ctx.Handler.Refuse = m => m.Message.MessageId == "f";
        ctx.Enqueue("f");
        QueueMessage a = ctx.Enqueue("a");

        Task pass = ctx.Queue.Trigger();
        await WaitUntil(() => ctx.Client.DeliveredIds.Count == 1);
        Assert.Equal(["a"], ctx.Client.DeliveredIds);

        await ctx.Queue.AcknowledgeDelivered(ctx.Client, a.Message.CreateAcknowledge());
        await pass.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(a.IsRemoved);
        Assert.Equal("f", ctx.Manager.MessageStore.ReadFirst().Message.MessageId);
        Assert.Equal(1, ctx.Handler.Offers("f"));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "condition was not met in time");
            await Task.Delay(5);
        }
    }

    private sealed class Context : IAsyncDisposable
    {
        public HorseQueue Queue;
        public TestManager Manager;
        public RefusingHandler Handler => Manager.Handler;
        public RecordingClient Client;
        public QueueClient Subscription;

        public static async Task<Context> Create(QueueAckDecision acknowledge)
        {
            HorseRider rider = new HorseRiderBuilder().Rider;
            rider.Queue.OptionsConfigurator = null;
            HorseQueue queue = new HorseQueue(rider, "refused-drain", new QueueOptions
            {
                Type = QueueType.Push,
                Acknowledge = acknowledge,
                AcknowledgeTimeout = TimeSpan.FromMinutes(1),
                CommitWhen = CommitWhen.None,
                PutBack = PutBackDecision.Regular
            });

            TestManager manager = new TestManager(queue);
            await queue.InitializeQueue(manager);

            // Subscribing triggers a drain; keep it from racing the explicit Trigger calls of the tests
            queue.SetStatus(QueueStatus.OnlyPush);
            RecordingClient client = new RecordingClient(rider);
            Assert.Equal(SubscriptionResult.Success, await queue.AddClient(client));
            typeof(HorseQueue).GetProperty(nameof(HorseQueue.Status))!.SetValue(queue, QueueStatus.Running);

            return new Context { Queue = queue, Manager = manager, Client = client, Subscription = queue.FindClient(client) };
        }

        public QueueMessage Enqueue(string id, bool priority = false)
        {
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, Queue.Name)
            {
                HighPriority = priority,
                WaitResponse = Queue.Options.Acknowledge != QueueAckDecision.None
            };
            message.SetMessageId(id);
            message.SetStringContent("payload");

            QueueMessage queueMessage = new QueueMessage(message);
            Manager.AddMessage(queueMessage);
            return queueMessage;
        }

        public async ValueTask DisposeAsync() => await Queue.Destroy();
    }

    private sealed class TestManager : MemoryQueueManager
    {
        public RefusingHandler Handler { get; }

        public TestManager(HorseQueue queue) : base(queue)
        {
            Handler = new RefusingHandler(this);
            DeliveryHandler = Handler;
        }
    }

    private sealed class RefusingHandler(IHorseQueueManager manager) : MemoryDeliveryHandler(manager)
    {
        private readonly Dictionary<string, int> _offers = new();

        public Func<QueueMessage, bool> Refuse = _ => false;
        public int TotalOffers => _offers.Values.Sum();
        public int Offers(string id) => _offers.GetValueOrDefault(id);

        public override Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            _offers[message.Message.MessageId] = Offers(message.Message.MessageId) + 1;
            return base.BeginSend(queue, message);
        }

        public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
            => Task.FromResult(!Refuse(message));
    }

    private sealed class RecordingClient : MessagingClient
    {
        public List<string> DeliveredIds { get; } = new();

        public RecordingClient(HorseRider rider) : base(rider, new Connection())
        {
            IsConnected = true;
        }

        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (message.Type == MessageType.QueueMessage)
                lock (DeliveredIds)
                    DeliveredIds.Add(message.MessageId);

            return Task.FromResult(true);
        }
    }

    private sealed class Connection : IConnectionInfo
    {
        public TcpClient Client => null;
        public bool IsSsl => false;
        public ConnectionStates State { get; set; }
        public IHorseProtocol Protocol { get; set; }
        public SocketBase Socket { get; set; }
        public Stream GetStream() => Stream.Null;
        public void Close() { }
    }
}
