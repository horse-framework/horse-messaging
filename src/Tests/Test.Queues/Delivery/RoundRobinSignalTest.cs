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
using Horse.Messaging.Server.Queues.States;
using Xunit;

namespace Test.Queues.Delivery;

public class RoundRobinSignalTest
{
    [Fact]
    public async Task HistoricalAcknowledges_DoNotCauseRepeatedConsumerScans()
    {
        await using var ctx = await Context.Create();
        Parallel.For(0, 100_000, _ => ctx.State.SignalClientAvailable());

        Task<PushResult> push = ctx.State.Push(ctx.Message());
        try
        {
            // Two initial scans and one pending wake-up, regardless of the ACK history.
            Assert.Equal(3, ctx.Handler.Scans);
            Assert.False(push.IsCompleted);
        }
        finally
        {
            ctx.Handler.AllowReceive = true;
            ctx.State.SignalClientAvailable();
            Assert.Equal(PushResult.Success, await push.WaitAsync(TimeSpan.FromSeconds(2)));
        }

        Assert.Equal(1, ctx.Client.Deliveries);
    }

    [Fact]
    public async Task SignalBetweenScanAndWait_IsRetained()
    {
        await using var ctx = await Context.Create();
        ctx.Handler.OnScan = scan =>
        {
            if (scan != 2)
                return;

            // Availability changes after this scan rejected the consumer, before waiting.
            ctx.Handler.AllowReceive = true;
            ctx.State.SignalClientAvailable();
        };

        Assert.Equal(PushResult.Success,
            await ctx.State.Push(ctx.Message()).WaitAsync(TimeSpan.FromMilliseconds(500)));

        Assert.Equal(3, ctx.Handler.Scans);
        Assert.Equal(1, ctx.Client.Deliveries);
    }

    [Fact]
    public async Task SignalWhileWaiting_WakesWithoutTimeoutFallback()
    {
        await using var ctx = await Context.Create();
        Task<PushResult> push = ctx.State.Push(ctx.Message());
        Assert.Equal(2, ctx.Handler.Scans);
        Assert.False(push.IsCompleted);

        ctx.Handler.AllowReceive = true;
        ctx.State.SignalClientAvailable();

        Assert.Equal(PushResult.Success, await push.WaitAsync(TimeSpan.FromMilliseconds(500)));
        Assert.Equal(3, ctx.Handler.Scans);
        Assert.Equal(1, ctx.Client.Deliveries);
    }

    [Fact]
    public async Task TimedOutWait_DoesNotConsumeTheNextSignal()
    {
        await using var ctx = await Context.Create();
        ctx.Handler.OnScan = scan =>
        {
            if (scan != 3)
                return;

            // The first timed wait has expired. Its next scan rejects the consumer just
            // before a new availability signal; that signal must survive the timeout.
            ctx.Handler.AllowReceive = true;
            ctx.State.SignalClientAvailable();
        };

        Assert.Equal(PushResult.Success,
            await ctx.State.Push(ctx.Message()).WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.Equal(4, ctx.Handler.Scans);
        Assert.Equal(1, ctx.Client.Deliveries);
    }

    private sealed class Context : IAsyncDisposable
    {
        public HorseQueue Queue;
        public FilteringHandler Handler;
        public RecordingClient Client;
        public RoundRobinQueueState State => (RoundRobinQueueState) Queue.State;

        public static async Task<Context> Create()
        {
            HorseRider rider = new HorseRiderBuilder().Rider;
            rider.Queue.OptionsConfigurator = null;
            var queue = new HorseQueue(rider, "rr-signal", new QueueOptions
            {
                Type = QueueType.RoundRobin,
                Acknowledge = QueueAckDecision.None,
                CommitWhen = CommitWhen.None
            });
            var manager = new TestManager(queue);
            await queue.InitializeQueue(manager);
            // Exercise State.Push directly without the background drain consuming messages.
            queue.SetStatus(QueueStatus.OnlyPush);
            var client = new RecordingClient(rider);
            Assert.Equal(SubscriptionResult.Success, await queue.AddClient(client));
            return new Context { Queue = queue, Handler = manager.Handler, Client = client };
        }

        public QueueMessage Message()
        {
            var message = new HorseMessage(MessageType.QueueMessage, Queue.Name);
            message.SetMessageId(Guid.NewGuid().ToString("N"));
            return new QueueMessage(message);
        }

        public async ValueTask DisposeAsync() => await Queue.Destroy();
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
        public volatile bool AllowReceive;
        public int Scans;
        public Action<int> OnScan;

        public FilteringHandler(IHorseQueueManager manager) : base(manager) { }

        public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            bool allowed = AllowReceive;
            int scan = ++Scans;
            OnScan?.Invoke(scan);
            return Task.FromResult(allowed);
        }
    }

    private sealed class RecordingClient : MessagingClient
    {
        public int Deliveries;

        public RecordingClient(HorseRider rider) : base(rider, new TestConnection())
        {
            IsConnected = true;
        }

        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
        {
            if (message.Type == MessageType.QueueMessage)
                Deliveries++;
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
