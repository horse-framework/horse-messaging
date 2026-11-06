using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Queues;
using Test.Common;
using Xunit;

namespace Test.Channels;

public class ChannelDeliveryTest
{
    [Fact]
    public async Task PublishIsDeliveredToEverySubscriber()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient[] subscribers = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => ChannelTestClient.Connect(port)));
            foreach (ChannelTestClient s in subscribers)
                Assert.Equal(HorseResultCode.Ok, (await s.Client.Channel.Subscribe("fanout", true, CancellationToken.None)).Code);

            ChannelTestClient publisher = await ChannelTestClient.Connect(port);
            HorseResult result = await publisher.Client.Channel.PublishString("fanout", "hello", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            foreach (ChannelTestClient s in subscribers)
            {
                await s.WaitForCount(1);
                Assert.Equal(new[] { "hello" }, s.Contents);
                Assert.Equal("fanout", s.Received.Single().Target);
            }

            //publisher is not a subscriber
            Assert.Empty(publisher.Received);

            HorseChannel channel = server.Rider.Channel.Find("fanout");
            await TestHorseRider.WaitUntil(() => channel.Info.Published == 1);
            Assert.Equal(1, channel.Info.Published);
            Assert.Equal(3, channel.Info.Received);
        });
    }

    [Fact]
    public async Task MessagesAreDeliveredInPublishOrder()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("ordered", true, CancellationToken.None);

            HorseChannel channel = server.Rider.Channel.Find("ordered");
            const int count = 50;
            for (int i = 0; i < count; i++)
                Assert.Equal(PushResult.Success, channel.Push("m" + i));

            await subscriber.WaitForCount(count);
            Assert.Equal(Enumerable.Range(0, count).Select(i => "m" + i), subscriber.Contents);
        });
    }

    [Fact]
    public async Task UnsubscribedClientDoesNotReceive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient stays = await ChannelTestClient.Connect(port);
            ChannelTestClient leaves = await ChannelTestClient.Connect(port);
            await stays.Client.Channel.Subscribe("unsub", true, CancellationToken.None);
            await leaves.Client.Channel.Subscribe("unsub", true, CancellationToken.None);

            HorseChannel channel = server.Rider.Channel.Find("unsub");
            Assert.Equal(2, channel.ClientsCount());

            Assert.Equal(HorseResultCode.Ok, (await leaves.Client.Channel.Unsubscribe("unsub", true, CancellationToken.None)).Code);
            Assert.Equal(1, channel.ClientsCount());

            channel.Push("after");
            await stays.WaitForCount(1);
            await Task.Delay(200);

            Assert.Single(stays.Received);
            Assert.Empty(leaves.Received);
        });
    }

    [Fact]
    public async Task DisconnectedClientIsRemovedFromChannel()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient client = await ChannelTestClient.Connect(port);
            await client.Client.Channel.Subscribe("disconnect", true, CancellationToken.None);

            HorseChannel channel = server.Rider.Channel.Find("disconnect");
            Assert.Equal(1, channel.ClientsCount());

            client.Client.Disconnect();
            await TestHorseRider.WaitUntil(() => channel.ClientsCount() == 0);

            Assert.Equal(0, channel.ClientsCount());
            Assert.Equal(0, channel.Info.SubscriberCount);
        });
    }

    [Fact]
    public async Task LastMessageIsSentToNewSubscriberWhenEnabled()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("initial", o => o.SendLastMessageAsInitial = true);
            channel.Push("first");
            channel.Push("last");
            await TestHorseRider.WaitUntil(() => channel.Info.Published == 2);

            HorseMessage initial = await channel.GetInitialMessage();
            Assert.Equal("last", initial.GetStringContent());

            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("initial", true, CancellationToken.None);

            await subscriber.WaitForCount(1);
            await Task.Delay(200);
            Assert.Equal(new[] { "last" }, subscriber.Contents);
        });
    }

    [Fact]
    public async Task LastMessageIsNotSentWhenDisabled()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("no-initial", o => o.SendLastMessageAsInitial = false);
            channel.Push("old");
            await TestHorseRider.WaitUntil(() => channel.Info.Published == 1);

            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("no-initial", true, CancellationToken.None);
            await Task.Delay(300);

            Assert.Empty(subscriber.Received);
        });
    }

    [Fact]
    public async Task MessageLargerThanSizeLimitIsRejected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("sized", o => o.MessageSizeLimit = 10);

            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("sized", true, CancellationToken.None);

            ChannelTestClient publisher = await ChannelTestClient.Connect(port);
            HorseResult tooLarge = await publisher.Client.Channel.PublishString("sized", new string('x', 64), true, CancellationToken.None);
            HorseResult small = await publisher.Client.Channel.PublishString("sized", "ok", true, CancellationToken.None);

            Assert.NotEqual(HorseResultCode.Ok, tooLarge.Code);
            Assert.Equal(HorseResultCode.Ok, small.Code);

            await subscriber.WaitForCount(1);
            await Task.Delay(200);
            Assert.Equal(new[] { "ok" }, subscriber.Contents);
            Assert.Equal(PushResult.LimitExceeded, channel.Push(new string('y', 64)));
        });
    }

    [Fact]
    public async Task PausedChannelRejectsPublishes()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("paused");
            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("paused", true, CancellationToken.None);

            channel.Status = ChannelStatus.Paused;
            Assert.Equal(PushResult.StatusNotSupported, channel.Push("while paused"));

            ChannelTestClient publisher = await ChannelTestClient.Connect(port);
            HorseResult result = await publisher.Client.Channel.PublishString("paused", "from client", true, CancellationToken.None);
            Assert.NotEqual(HorseResultCode.Ok, result.Code);

            channel.Status = ChannelStatus.Running;
            Assert.Equal(PushResult.Success, channel.Push("running"));

            await subscriber.WaitForCount(1);
            await Task.Delay(200);
            Assert.Equal(new[] { "running" }, subscriber.Contents);
        });
    }

    [Fact]
    public async Task OperationalHeadersAreRemovedAndCustomHeadersKept()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("headers", true, CancellationToken.None);

            ChannelTestClient publisher = await ChannelTestClient.Connect(port);
            List<KeyValuePair<string, string>> headers = new()
            {
                new(HorseHeaders.CC, "someone"),
                new(HorseHeaders.CHANNEL_NAME, "headers"),
                new("X-Custom", "kept")
            };

            await publisher.Client.Channel.PublishString("headers", "content", true, headers, CancellationToken.None);
            await subscriber.WaitForCount(1);

            HorseMessage received = subscriber.Received.Single();
            Assert.Null(received.FindHeader(HorseHeaders.CC));
            Assert.Null(received.FindHeader(HorseHeaders.CHANNEL_NAME));
            Assert.Equal("kept", received.FindHeader("X-Custom"));
        });
    }

    [Fact]
    public async Task InitialMessageIsNeverOutOfOrderOrDuplicated()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("initial-order", o => o.SendLastMessageAsInitial = true);

            //publishes a growing sequence while clients subscribe in the middle of it
            using CancellationTokenSource cts = new CancellationTokenSource();
            Task publisher = Task.Run(async () =>
            {
                int i = 0;
                while (!cts.IsCancellationRequested)
                {
                    channel.Push(i++.ToString());
                    if (i % 20 == 0)
                        await Task.Delay(1);
                }
            });

            List<ChannelTestClient> subscribers = new();
            for (int i = 0; i < 10; i++)
            {
                ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
                await subscriber.Client.Channel.Subscribe("initial-order", true, CancellationToken.None);
                subscribers.Add(subscriber);
                await Task.Delay(20);
            }

            await Task.Delay(200);
            cts.Cancel();
            await publisher;
            await Task.Delay(300);

            foreach (ChannelTestClient subscriber in subscribers)
            {
                int[] sequence = subscriber.Contents.Select(int.Parse).ToArray();
                Assert.NotEmpty(sequence);

                //strictly increasing: the initial message is not delivered after newer ones, nor twice.
                //gaps are possible, per client send queue drops oldest when the client is slower than the publisher
                for (int i = 1; i < sequence.Length; i++)
                    Assert.True(sequence[i] > sequence[i - 1], $"out of order at {i}: {sequence[i - 1]} then {sequence[i]}");
            }
        });
    }
}
