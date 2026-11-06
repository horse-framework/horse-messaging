using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Test.Common;
using Xunit;

namespace Test.Channels;

public class ChannelRiderTest
{
    private static async Task<HorseClient> Connect(int port)
    {
        return (await ChannelTestClient.Connect(port)).Client;
    }

    [Fact]
    public async Task ChannelLimitAllowsChannelsUpToTheLimit()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            server.Rider.Options.ChannelLimit = 2;

            Assert.NotNull(await server.Rider.Channel.Create("limit-1"));
            Assert.NotNull(await server.Rider.Channel.Create("limit-2"));
            await Assert.ThrowsAsync<OperationCanceledException>(() => server.Rider.Channel.Create("limit-3"));
            Assert.Null(server.Rider.Channel.Find("limit-3"));
        });
    }

    [Fact]
    public async Task ClientLimitOfOneAcceptsOneSubscriber()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Channel.Create("single", o => o.ClientLimit = 1);

            HorseClient first = await Connect(port);
            HorseClient second = await Connect(port);

            HorseResult firstResult = await first.Channel.Subscribe("single", true, CancellationToken.None);
            HorseResult secondResult = await second.Channel.Subscribe("single", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, firstResult.Code);
            Assert.NotEqual(HorseResultCode.Ok, secondResult.Code);
            Assert.Equal(1, server.Rider.Channel.Find("single").ClientsCount());

            first.Disconnect();
            second.Disconnect();
        });
    }

    [Fact]
    public async Task UnlimitedChannelAcceptsMoreThanInitialCapacity()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("unlimited", o => o.ClientLimit = 0);

            const int count = 300;
            HorseClient[] clients = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => Connect(port)));
            HorseResult[] results = await Task.WhenAll(clients.Select(c => c.Channel.Subscribe("unlimited", true, CancellationToken.None)));

            Assert.All(results, r => Assert.Equal(HorseResultCode.Ok, r.Code));
            Assert.Equal(count, channel.ClientsCount());

            foreach (HorseClient client in clients)
                client.Disconnect();
        });
    }

    [Fact]
    public async Task SubscribingTwiceDoesNotDuplicateDelivery()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("twice");
            HorseClient client = await Connect(port);

            int received = 0;
            client.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.Channel)
                    Interlocked.Increment(ref received);
            };

            Assert.Equal(HorseResultCode.Ok, (await client.Channel.Subscribe("twice", true, CancellationToken.None)).Code);
            Assert.Equal(HorseResultCode.Ok, (await client.Channel.Subscribe("twice", true, CancellationToken.None)).Code);
            Assert.Equal(1, channel.ClientsCount());

            channel.Push("hello");
            await TestHorseRider.WaitUntil(() => received > 0);
            await Task.Delay(200);

            Assert.Equal(1, received);
            client.Disconnect();
        });
    }

    [Fact]
    public async Task ServerSidePushIsDeliveredAsChannelMessage()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("server-push");
            HorseClient client = await Connect(port);

            HorseMessage received = null;
            client.MessageReceived += (_, m) => received ??= m;

            await client.Channel.Subscribe("server-push", true, CancellationToken.None);
            channel.Push("payload");

            await TestHorseRider.WaitUntil(() => received != null);

            Assert.NotNull(received);
            Assert.Equal(MessageType.Channel, received.Type);
            Assert.Equal("server-push", received.Target);
            Assert.Equal("payload", received.GetStringContent());
            client.Disconnect();
        });
    }

    [Fact]
    public async Task RemoveByNameDestroysChannelAndEndsSubscriptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("remove-by-name");
            HorseClient client = await Connect(port);
            await client.Channel.Subscribe("remove-by-name", true, CancellationToken.None);

            MessagingClient messagingClient = server.Rider.Client.Find(client.ClientId);
            Assert.Single(messagingClient.GetChannels());

            HorseChannel removed = server.Rider.Channel.Remove("remove-by-name");

            Assert.Same(channel, removed);
            Assert.Null(server.Rider.Channel.Find("remove-by-name"));
            Assert.Equal(ChannelStatus.Destroyed, channel.Status);
            Assert.Equal(0, channel.ClientsCount());
            Assert.Empty(messagingClient.GetChannels());
            Assert.NotEqual(Horse.Messaging.Server.Queues.PushResult.Success, channel.Push("after destroy"));

            client.Disconnect();
        });
    }

    [Fact]
    public async Task RemovingStaleInstanceKeepsNewChannelWithSameName()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            HorseChannel old = await server.Rider.Channel.Create("same-name");
            server.Rider.Channel.Remove(old);

            HorseChannel fresh = await server.Rider.Channel.Create("same-name");
            server.Rider.Channel.Remove(old);

            Assert.Same(fresh, server.Rider.Channel.Find("same-name"));
            Assert.Equal(ChannelStatus.Running, fresh.Status);
        });
    }

    [Fact]
    public async Task SteadySubscriberReceivesEveryMessageWhileOthersChurn()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("churn");

            List<HorseClient> churners = new List<HorseClient>();
            for (int i = 0; i < 8; i++)
            {
                HorseClient c = await Connect(port);
                await c.Channel.Subscribe("churn", true, CancellationToken.None);
                churners.Add(c);
            }

            HorseClient steady = await Connect(port);
            int received = 0;
            steady.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.Channel)
                    Interlocked.Increment(ref received);
            };
            await steady.Channel.Subscribe("churn", true, CancellationToken.None);

            using CancellationTokenSource cts = new CancellationTokenSource();
            Task churn = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    foreach (HorseClient c in churners)
                    {
                        await c.Channel.Unsubscribe("churn", true, CancellationToken.None);
                        await c.Channel.Subscribe("churn", true, CancellationToken.None);
                    }
                }
            });

            const int messageCount = 300;
            for (int i = 0; i < messageCount; i++)
            {
                channel.Push("m" + i);
                //per client send queue is bounded (drop oldest), keep the publisher slower than delivery
                if (i % 20 == 0)
                    await Task.Delay(10);
            }

            await TestHorseRider.WaitUntil(() => received >= messageCount, 5000);
            cts.Cancel();
            await churn;

            Assert.Equal(messageCount, received);

            steady.Disconnect();
            foreach (HorseClient c in churners)
                c.Disconnect();
        });
    }
}
