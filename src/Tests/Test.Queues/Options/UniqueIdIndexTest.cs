using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Xunit;

namespace Test.Queues.Options;

/// <summary>
/// The unique id index must follow the queue's content: ids of cleared messages are free again,
/// loaded and replicated messages keep theirs, and a push that never reached the queue keeps none.
/// </summary>
public class UniqueIdIndexTest : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "uid-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_path, true);
        }
        catch
        {
        }
    }

    private HorseRider CreateRider(string mode)
    {
        Directory.CreateDirectory(_path);
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = _path)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.MessageIdUniqueCheck = true;

                if (mode == "persistent")
                    q.UsePersistentQueues(db => db.SetPhysicalPath(queue => Path.Combine(_path, $"{queue.Name}.hdb"))
                        .UseInstantFlush()
                        .SetAutoShrink(false));
                else
                    q.UseMemoryQueues();
            })
            .Build();
    }

    private static HorseMessage Message(string id)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "x");
        message.SetMessageId(id);
        message.SetStringContent("payload");
        return message;
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClearMessages_FreesIdsOfClearedMessages(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("clear-ids");
        Assert.Equal(PushResult.Success, await queue.Push(Message("a")));

        queue.ClearMessages();

        Assert.Equal(PushResult.Success, await queue.Push(Message("a")));
        Assert.Equal(1, queue.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task Restart_LoadedMessagesKeepTheirIds()
    {
        HorseRider rider = CreateRider("persistent");
        HorseQueue queue = await rider.Queue.Create("restart-ids");
        Assert.Equal(PushResult.Success, await queue.Push(Message("kept")));
        await queue.Manager.Flush();

        HorseRider restarted = CreateRider("persistent");
        HorseQueue loaded = restarted.Queue.Find("restart-ids");
        try
        {
            Assert.Equal(PushResult.DuplicateUniqueId, await loaded.Push(Message("kept")));
            Assert.Equal(1, loaded.Manager.MessageStore.Count());
        }
        finally
        {
            await loaded.Manager.Flush();
        }
    }

    [Fact]
    public async Task ReplicatedPush_KeepsItsId()
    {
        HorseRider rider = CreateRider("memory");
        HorseQueue queue = await rider.Queue.Create("replica-ids");
        HorseMessage replicated = Message("replicated");
        replicated.SetTarget(queue.Name);

        Assert.Equal(PushResult.Success, await queue.PushByNode(replicated));
        Assert.Equal(PushResult.DuplicateUniqueId, await queue.Push(Message("replicated")));
    }

    [Fact]
    public async Task PushThatNeverReachedTheQueue_ReleasesItsId()
    {
        HorseRider rider = new HorseRiderBuilder().Rider;
        rider.Queue.OptionsConfigurator = null;
        HorseQueue queue = new HorseQueue(rider, "failed-push-ids", new QueueOptions
        {
            Type = QueueType.Push,
            Acknowledge = QueueAckDecision.None,
            CommitWhen = CommitWhen.None,
            MessageIdUniqueCheck = true
        });
        RejectingManager manager = new RejectingManager(queue);
        await queue.InitializeQueue(manager);

        try
        {
            // e.g. a persistent queue that could not save the message
            manager.Handler.RejectNext = true;
            Assert.Equal(PushResult.StatusNotSupported, await queue.Push(Message("retry")));
            Assert.Equal(0, queue.Manager.MessageStore.Count());

            // the producer's retry with the same id
            Assert.Equal(PushResult.Success, await queue.Push(Message("retry")));
            Assert.Equal(1, queue.Manager.MessageStore.Count());
        }
        finally
        {
            await queue.Destroy();
        }
    }

    [Fact]
    public async Task PullWithClear_FreesIdsOfClearedMessages()
    {
        HorseRider rider = new HorseRiderBuilder().Rider;
        rider.Queue.OptionsConfigurator = null;
        HorseQueue queue = new HorseQueue(rider, "pull-clear-ids", new QueueOptions
        {
            Type = QueueType.Pull,
            Acknowledge = QueueAckDecision.None,
            CommitWhen = CommitWhen.None,
            MessageIdUniqueCheck = true
        });
        await queue.InitializeQueue(new MemoryQueueManager(queue));

        try
        {
            for (int i = 0; i < 3; i++)
                Assert.Equal(PushResult.Success, await queue.Push(Message("m" + i)));

            PullClient client = new PullClient(rider);
            Assert.Equal(SubscriptionResult.Success, await queue.AddClient(client));

            HorseMessage request = new HorseMessage(MessageType.Server, queue.Name);
            request.SetMessageId(Guid.NewGuid().ToString("N"));
            request.AddHeader(HorseHeaders.COUNT, "1");
            request.AddHeader(HorseHeaders.CLEAR, "all");
            await queue.State.Pull(queue.FindClient(client), request);

            Assert.True(queue.IsEmpty);
            Assert.Equal(PushResult.Success, await queue.Push(Message("m1")));
            Assert.Equal(PushResult.Success, await queue.Push(Message("m2")));
        }
        finally
        {
            await queue.Destroy();
        }
    }

    private sealed class RejectingManager : MemoryQueueManager
    {
        public RejectingHandler Handler { get; }

        public RejectingManager(HorseQueue queue) : base(queue)
        {
            Handler = new RejectingHandler(this);
            DeliveryHandler = Handler;
        }
    }

    private sealed class RejectingHandler(IHorseQueueManager manager) : MemoryDeliveryHandler(manager)
    {
        public bool RejectNext;

        public override Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (!RejectNext)
                return base.ReceivedFromProducer(queue, message, sender);

            RejectNext = false;
            return Task.FromResult(Decision.InterruptFlow(false, DecisionTransmission.Failed));
        }
    }

    private sealed class PullClient : MessagingClient
    {
        public PullClient(HorseRider rider) : base(rider, new Connection())
        {
            IsConnected = true;
        }

        public override Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
            => Task.FromResult(true);
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
