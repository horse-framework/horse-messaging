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
/// A send to the consumer fails (it is disconnecting). Pull and round robin queues used to lose
/// the message, and a pull queue waiting for acknowledge kept its lock forever: no delivery was
/// tracked, so no ACK or timeout released it, and every later pull of the queue hung.
/// </summary>
public class FailedSendTest
{
    #region Pull

    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task Pull_FailedSend_RetainsMessageAndNextPullIsServed(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.Pull, acknowledge);
        QueueMessage message = ctx.Enqueue("m1");
        ctx.Client.SendSucceeds = false;

        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(message, ctx.Manager.MessageStore.ReadFirst());
        Assert.False(message.IsRemoved);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());

        ctx.Client.SendSucceeds = true;
        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["m1"], ctx.Client.DeliveredIds);
        await ctx.AcknowledgeIfRequired(message);
        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60000)]
    public async Task Pull_FailedSend_HandlerPutBackIsAppliedOnce(int putBackDelay)
    {
        await using var ctx = await Context.Create(QueueType.Pull, QueueAckDecision.WaitForAcknowledge);
        ctx.Queue.Options.PutBackDelay = putBackDelay;
        ctx.Enqueue("m1");
        ctx.Client.SendSucceeds = false;
        ctx.Handler.FailureDecision = Decision.PutBackMessage(false);

        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(putBackDelay == 0 ? 1 : 0, ctx.Manager.PriorityMessageStore.Count() + ctx.Manager.MessageStore.Count());
        Assert.Equal(putBackDelay == 0 ? 0 : 1, ctx.Queue.GetMessageCountPendingForPutBack());

        // the lock is free: the next pull is answered instead of waiting forever
        ctx.Client.SendSucceeds = true;
        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Pull_FailedSend_HandlerDeleteIsRespected()
    {
        await using var ctx = await Context.Create(QueueType.Pull, QueueAckDecision.WaitForAcknowledge);
        QueueMessage message = ctx.Enqueue("m1");
        ctx.Client.SendSucceeds = false;
        ctx.Handler.FailureDecision = Decision.DeleteMessage();

        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
        Assert.Equal(PullResult.Empty, await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Pull_BeginSendStopsTheFlow_DoesNotHoldTheAcknowledgeLock()
    {
        await using var ctx = await Context.Create(QueueType.Pull, QueueAckDecision.WaitForAcknowledge);
        ctx.Enqueue("m1");
        ctx.Enqueue("m2");
        ctx.Handler.BeginDecision = Decision.InterruptFlow(false);

        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));

        ctx.Handler.BeginDecision = null;
        await ctx.Pull().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(["m2"], ctx.Client.DeliveredIds);
    }

    [Fact]
    public async Task Pull_FailedSendInBatch_StopsTheBatchAndKeepsTheRest()
    {
        await using var ctx = await Context.Create(QueueType.Pull, QueueAckDecision.None);
        ctx.Enqueue("m1");
        ctx.Enqueue("m2");
        ctx.Enqueue("m3");
        ctx.Client.FailAfter = 1;

        await ctx.Pull(3).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["m1"], ctx.Client.DeliveredIds);
        Assert.Equal(2, ctx.Manager.MessageStore.Count());
    }

    #endregion

    #region Round Robin

    [Theory]
    [InlineData(QueueAckDecision.None)]
    [InlineData(QueueAckDecision.WaitForAcknowledge)]
    public async Task RoundRobin_FailedSend_RetainsMessageAndEndsThePass(QueueAckDecision acknowledge)
    {
        await using var ctx = await Context.Create(QueueType.RoundRobin, acknowledge);
        QueueMessage message = ctx.Message("m1");
        ctx.Client.SendSucceeds = false;

        PushResult result = await ctx.Queue.State.Push(message);

        Assert.Equal(PushResult.NoConsumers, result);
        Assert.Same(message, ctx.Manager.MessageStore.ReadFirst());
        Assert.False(message.IsRemoved);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(0, ctx.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
        Assert.Null(ctx.Subscription.CurrentlyProcessing);

        ctx.Client.SendSucceeds = true;
        Assert.Same(message, ctx.Manager.MessageStore.ConsumeFirst());
        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));
        Assert.Equal(["m1"], ctx.Client.DeliveredIds);
        await ctx.AcknowledgeIfRequired(message);
        Assert.True(message.IsRemoved);
        Assert.True(ctx.Queue.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60000)]
    public async Task RoundRobin_FailedSend_HandlerPutBackIsAppliedOnce(int putBackDelay)
    {
        await using var ctx = await Context.Create(QueueType.RoundRobin, QueueAckDecision.WaitForAcknowledge);
        ctx.Queue.Options.PutBackDelay = putBackDelay;
        ctx.Client.SendSucceeds = false;
        ctx.Handler.FailureDecision = Decision.PutBackMessage(false);

        PushResult result = await ctx.Queue.State.Push(ctx.Message("m1"));

        Assert.Equal(PushResult.Success, result);
        Assert.Equal(0, ctx.Handler.EndSendCount);
        Assert.Equal(putBackDelay == 0 ? 1 : 0, ctx.Manager.PriorityMessageStore.Count() + ctx.Manager.MessageStore.Count());
        Assert.Equal(putBackDelay == 0 ? 0 : 1, ctx.Queue.GetMessageCountPendingForPutBack());
    }

    [Fact]
    public async Task RoundRobin_FailedSend_DoesNotCommitTheUnsentMessage()
    {
        await using var ctx = await Context.Create(QueueType.RoundRobin, QueueAckDecision.None, CommitWhen.AfterSent);
        RecordingClient producer = new RecordingClient(ctx.Queue.Rider);
        QueueMessage message = ctx.Message("m1");
        message.Source = producer;
        ctx.Client.SendSucceeds = false;

        await ctx.Queue.State.Push(message);
        Assert.Empty(producer.Acknowledges);

        ctx.Client.SendSucceeds = true;
        Assert.Same(message, ctx.Manager.MessageStore.ConsumeFirst());
        Assert.Equal(PushResult.Success, await ctx.Queue.State.Push(message));
        Assert.Single(producer.Acknowledges);
    }

    #endregion

    #region Context

    private sealed class Context : IAsyncDisposable
    {
        public HorseQueue Queue;
        public TestManager Manager;
        public FailingHandler Handler => Manager.Handler;
        public RecordingClient Client;
        public QueueClient Subscription;

        public static async Task<Context> Create(QueueType type, QueueAckDecision acknowledge, CommitWhen commitWhen = CommitWhen.None)
        {
            HorseRider rider = new HorseRiderBuilder().Rider;
            rider.Queue.OptionsConfigurator = null;
            HorseQueue queue = new HorseQueue(rider, "failed-send", new QueueOptions
            {
                Type = type,
                Acknowledge = acknowledge,
                AcknowledgeTimeout = TimeSpan.FromMinutes(1),
                CommitWhen = commitWhen,
                PutBack = PutBackDecision.Regular
            });

            TestManager manager = new TestManager(queue);
            await queue.InitializeQueue(manager);

            // States are driven directly; no background drain races the assertions
            if (type != QueueType.Pull)
                queue.SetStatus(QueueStatus.OnlyPush);

            RecordingClient client = new RecordingClient(rider);
            Assert.Equal(SubscriptionResult.Success, await queue.AddClient(client));
            return new Context { Queue = queue, Manager = manager, Client = client, Subscription = queue.FindClient(client) };
        }

        public QueueMessage Message(string id)
        {
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, Queue.Name)
            {
                WaitResponse = Queue.Options.Acknowledge != QueueAckDecision.None
            };
            message.SetMessageId(id);
            message.SetStringContent("payload");
            return new QueueMessage(message);
        }

        public QueueMessage Enqueue(string id)
        {
            QueueMessage message = Message(id);
            Manager.AddMessage(message);
            return message;
        }

        public Task<PullResult> Pull(int count = 1)
        {
            HorseMessage request = new HorseMessage(MessageType.Server, Queue.Name);
            request.SetMessageId(Guid.NewGuid().ToString("N"));
            request.AddHeader(HorseHeaders.COUNT, count.ToString());
            return Queue.State.Pull(Subscription, request);
        }

        public async Task AcknowledgeIfRequired(QueueMessage message)
        {
            if (Queue.Options.Acknowledge != QueueAckDecision.None)
                await Queue.AcknowledgeDelivered(Client, message.Message.CreateAcknowledge());
        }

        public async ValueTask DisposeAsync() => await Queue.Destroy();
    }

    private sealed class TestManager : MemoryQueueManager
    {
        public FailingHandler Handler { get; }

        public TestManager(HorseQueue queue) : base(queue)
        {
            Handler = new FailingHandler(this);
            DeliveryHandler = Handler;
        }
    }

    private sealed class FailingHandler(IHorseQueueManager manager) : MemoryDeliveryHandler(manager)
    {
        public int EndSendCount;
        public Decision? BeginDecision;
        public Decision? FailureDecision;

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
        public List<string> DeliveredIds { get; } = new();
        public List<HorseMessage> Acknowledges { get; } = new();
        public bool SendSucceeds = true;

        /// <summary>
        /// When set, sends of queue messages fail after this many succeeded
        /// </summary>
        public int? FailAfter;

        public RecordingClient(HorseRider rider) : base(rider, new Connection())
        {
            IsConnected = true;
        }

        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (message.Type == MessageType.Response)
            {
                Acknowledges.Add(message);
                return Task.FromResult(true);
            }

            if (message.Type != MessageType.QueueMessage || message.FindHeader(HorseHeaders.NO_CONTENT) != null)
                return Task.FromResult(true);

            if (!SendSucceeds || (FailAfter.HasValue && DeliveredIds.Count >= FailAfter.Value))
                return Task.FromResult(false);

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

    #endregion
}
