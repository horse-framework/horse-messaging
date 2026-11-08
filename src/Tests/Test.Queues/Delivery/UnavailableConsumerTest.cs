using System;
using System.Collections.Generic;
using System.IO;
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

public class UnavailableConsumerTest
{
    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task Pull_BlockedConsumer_DoesNotDequeueOrClearMessages(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Pull, acknowledge);
        QueueMessage first = ctx.Enqueue("first", true);
        ctx.Enqueue("second");
        ctx.Subscription.Blocked = true;
        HorseMessage request = ctx.PullRequest();
        request.AddHeader(HorseHeaders.CLEAR, "all");

        PullResult result = await ctx.Queue.State.Pull(ctx.Subscription, request);

        Assert.Equal(PullResult.Unacceptable, result);
        Assert.Same(first, ctx.Manager.PriorityMessageStore.ReadFirst());
        Assert.Equal(1, ctx.Manager.MessageStore.Count());
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
    }

    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task Pull_RejectedConsumer_RetainsMessageAndAllowsNextPull(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Pull, acknowledge);
        QueueMessage message = ctx.Enqueue("retained", true);
        ctx.Handler.AllowReceive = false;

        await ctx.Queue.State.Pull(ctx.Subscription, ctx.PullRequest());

        Assert.Same(message, ctx.Manager.PriorityMessageStore.ReadFirst());
        Assert.True(message.Message.HighPriority);
        Assert.False(message.IsRemoved);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());

        ctx.Handler.AllowReceive = true;
        await ctx.Queue.State.Pull(ctx.Subscription, ctx.PullRequest()).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(ctx.Client.Deliveries);
        Assert.Same(message.Message, ctx.Client.Deliveries[0]);
        await ctx.AcknowledgeIfRequired(message);
        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Fact]
    public async Task Pull_ConsumerBecomesBlockedDuringBatch_RetainsNextMessage()
    {
        await using var ctx = await Context.Create(QueueType.Pull, QueueAckDecision.None);
        ctx.Enqueue("first");
        QueueMessage second = ctx.Enqueue("second");
        ctx.Client.OnDelivery = _ => ctx.Subscription.Blocked = true;

        await ctx.Queue.State.Pull(ctx.Subscription, ctx.PullRequest(2));

        Assert.Single(ctx.Client.Deliveries);
        Assert.Same(second, ctx.Manager.MessageStore.ReadFirst());
        Assert.True(second.IsInQueue);
        Assert.False(second.IsRemoved);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
    }

    [Theory]
    [InlineData("blocked", QueueAckDecision.None)]
    [InlineData("disconnected", QueueAckDecision.None)]
    [InlineData("filtered", QueueAckDecision.None)]
    [InlineData("blocked", QueueAckDecision.WaitForAcknowledge)]
    [InlineData("disconnected", QueueAckDecision.WaitForAcknowledge)]
    [InlineData("filtered", QueueAckDecision.WaitForAcknowledge)]
    public async Task Push_NoEligibleConsumer_RetainsMessageForLaterDelivery(string reason, QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Push, acknowledge);
        var message = ctx.Message("retry", true);
        // A retry can have succeeded on a previous attempt; SendCount is a lifetime count.
        message.MarkAsSent();
        ctx.Subscription.Blocked = reason == "blocked";
        ctx.Client.SetConnected(reason != "disconnected");
        ctx.Handler.AllowReceive = reason != "filtered";

        PushResult result = await ctx.Queue.State.Push(message);

        Assert.Equal(PushResult.NoConsumers, result);
        Assert.Same(message, ctx.Manager.PriorityMessageStore.ReadFirst());
        Assert.True(message.Message.HighPriority);
        Assert.False(message.IsRemoved);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());

        ctx.Subscription.Blocked = false;
        ctx.Client.SetConnected(true);
        ctx.Handler.AllowReceive = true;
        Assert.Same(message, ctx.Manager.PriorityMessageStore.ConsumeFirst());
        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));
        Assert.Single(ctx.Client.Deliveries);
        await ctx.AcknowledgeIfRequired(message);
        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Fact]
    public async Task Push_NoEligibleConsumer_DoesNotUndoExplicitDeleteDecision()
    {
        await using var ctx = await Context.Create(QueueType.Push, QueueAckDecision.None);
        QueueMessage message = ctx.Message("deleted");
        ctx.Subscription.Blocked = true;
        ctx.Handler.BeginDecision = Decision.DeleteMessage();

        await ctx.Queue.State.Push(message);

        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
        Assert.Empty(ctx.Client.Deliveries);
    }

    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task Push_AllSendsFail_RetainsMessageForSuccessfulRetry(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Push, acknowledge);
        QueueMessage message = ctx.Message("failed-send", true);
        // Earlier delivery success must not hide failure of this particular attempt.
        message.MarkAsSent();
        ctx.Client.SendSucceeds = false;

        PushResult result = await ctx.Queue.State.Push(message);

        Assert.Equal(PushResult.NoConsumers, result);
        Assert.Same(message, ctx.Manager.PriorityMessageStore.ReadFirst());
        Assert.True(message.Message.HighPriority);
        Assert.False(message.IsRemoved);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());

        ctx.Client.SendSucceeds = true;
        Assert.Same(message, ctx.Manager.PriorityMessageStore.ConsumeFirst());
        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));
        Assert.Single(ctx.Client.Deliveries);
        await ctx.AcknowledgeIfRequired(message);
        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Theory]
    [InlineData(QueueAckDecision.None, 0)]
    [InlineData(QueueAckDecision.None, 60000)]
    [InlineData(QueueAckDecision.WaitForAcknowledge, 0)]
    [InlineData(QueueAckDecision.WaitForAcknowledge, 60000)]
    public async Task Push_NoEligibleConsumer_PreservesAlreadyAppliedPutBack(QueueAckDecision acknowledge, int delay)
    {
        await using var ctx = await Context.Create(QueueType.Push, acknowledge);
        QueueMessage message = ctx.Message("handler-putback", true);
        ctx.Subscription.Blocked = true;
        ctx.Queue.Options.PutBackDelay = delay;
        ctx.Handler.BeginDecision = Decision.PutBackMessage(false);
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge();

        PushResult result = await ctx.Queue.State.Push(message);

        Assert.Equal(PushResult.Success, result);
        Assert.False(message.IsRemoved);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(delay == 0 ? 1 : 0, ctx.Manager.PriorityMessageStore.Count());
        Assert.Equal(delay == 0 ? 0 : 1, ctx.Queue.GetMessageCountPendingForPutBack());
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task Push_AllSendsFail_RespectsExplicitFailureDelete(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Push, acknowledge);
        QueueMessage message = ctx.Message("failure-delete", true);
        ctx.Client.SendSucceeds = false;
        ctx.Handler.FailureDecision = Decision.DeleteMessage();
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge();

        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));

        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(QueueAckDecision.None, 0)]
    [InlineData(QueueAckDecision.None, 60000)]
    [InlineData(QueueAckDecision.WaitForAcknowledge, 0)]
    [InlineData(QueueAckDecision.WaitForAcknowledge, 60000)]
    public async Task Push_AllSendsFail_AppliesFailurePutBackOnce(QueueAckDecision acknowledge, int delay)
    {
        await using var ctx = await Context.Create(QueueType.Push, acknowledge);
        QueueMessage message = ctx.Message("failure-putback", true);
        ctx.Client.SendSucceeds = false;
        ctx.Queue.Options.PutBackDelay = delay;
        ctx.Handler.FailureDecision = Decision.PutBackMessage(false);
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge();

        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));

        Assert.False(message.IsRemoved);
        Assert.Empty(ctx.Client.Deliveries);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(delay == 0 ? 1 : 0, ctx.Manager.PriorityMessageStore.Count());
        Assert.Equal(delay == 0 ? 0 : 1, ctx.Queue.GetMessageCountPendingForPutBack());
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
        if (acknowledge == QueueAckDecision.WaitForAcknowledge)
            await ctx.Queue.WaitForAcknowledge().WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class Context : IAsyncDisposable
    {
        public HorseQueue Queue;
        public TestManager Manager;
        public FilteringHandler Handler => Manager.Handler;
        public RecordingClient Client;
        public QueueClient Subscription;

        public static async Task<Context> Create(QueueType type, QueueAckDecision acknowledge)
        {
            HorseRider rider = new HorseRiderBuilder().Rider;
            rider.Queue.OptionsConfigurator = null;
            var queue = new HorseQueue(rider, "unavailable-consumer", new QueueOptions
            {
                Type = type,
                Acknowledge = acknowledge,
                AcknowledgeTimeout = TimeSpan.FromMinutes(1),
                CommitWhen = CommitWhen.None,
                PutBack = PutBackDecision.Priority
            });
            var manager = new TestManager(queue);
            await queue.InitializeQueue(manager);
            // Exercise the state directly, without background draining racing the assertions.
            if (type == QueueType.Push)
                queue.SetStatus(QueueStatus.OnlyPush);
            var client = new RecordingClient(rider);
            Assert.Equal(SubscriptionResult.Success, await queue.AddClient(client));
            return new Context { Queue = queue, Manager = manager, Client = client, Subscription = queue.FindClient(client) };
        }

        public QueueMessage Message(string id, bool highPriority = false)
        {
            var message = new HorseMessage(MessageType.QueueMessage, Queue.Name)
            {
                HighPriority = highPriority,
                WaitResponse = Queue.Options.Acknowledge != QueueAckDecision.None
            };
            message.SetMessageId(id);
            message.SetStringContent("payload");
            return new QueueMessage(message);
        }

        public QueueMessage Enqueue(string id, bool highPriority = false)
        {
            QueueMessage message = Message(id, highPriority);
            Manager.AddMessage(message);
            return message;
        }

        public HorseMessage PullRequest(int count = 1)
        {
            var request = new HorseMessage(MessageType.Server, Queue.Name);
            request.SetMessageId(Guid.NewGuid().ToString("N"));
            request.AddHeader(HorseHeaders.COUNT, count.ToString());
            return request;
        }

        public async Task AcknowledgeIfRequired(QueueMessage message)
        {
            if (Queue.Options.Acknowledge != QueueAckDecision.None)
                await Queue.AcknowledgeDelivered(Client, message.Message.CreateAcknowledge());
        }

        public async ValueTask DisposeAsync()
        {
            await Queue.Destroy();
        }
    }

    private sealed class TestManager : MemoryQueueManager
    {
        public FilteringHandler Handler { get; }

        public TestManager(HorseQueue queue) : base(queue)
        {
            Handler = new FilteringHandler(this);
            DeliveryHandler = Handler;
        }
    }

    private sealed class FilteringHandler : MemoryDeliveryHandler
    {
        public bool AllowReceive = true;
        public int EndSendCount;
        public Decision? BeginDecision;
        public Decision? FailureDecision;

        public FilteringHandler(IHorseQueueManager manager) : base(manager) { }

        public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
            => Task.FromResult(AllowReceive);

        public override Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
            => BeginDecision.HasValue ? Task.FromResult(BeginDecision.Value) : base.BeginSend(queue, message);

        public override Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
            => FailureDecision.HasValue ? Task.FromResult(FailureDecision.Value) : base.ConsumerReceiveFailed(queue, delivery, receiver);

        public override Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            EndSendCount++;
            return base.EndSend(queue, message);
        }
    }

    private sealed class RecordingClient : MessagingClient
    {
        public List<HorseMessage> Deliveries { get; } = new();
        public Action<HorseMessage> OnDelivery;
        public bool SendSucceeds = true;

        public RecordingClient(HorseRider rider) : base(rider, new TestConnection()) { }

        public void SetConnected(bool connected) => IsConnected = connected;

        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (message.Type == MessageType.QueueMessage && message.FindHeader(HorseHeaders.NO_CONTENT) == null)
            {
                if (!SendSucceeds)
                    return Task.FromResult(false);
                Deliveries.Add(message);
                OnDelivery?.Invoke(message);
            }

            return Task.FromResult(true);
        }
    }

    private sealed class TestConnection : IConnectionInfo
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
