using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Test.Common;
using Xunit;

namespace Test.Channels;

public class ChannelManagementTest
{
    private class ChannelAuthorization : IChannelAuthorization
    {
        public bool AllowPush { get; set; } = true;
        public bool AllowSubscribe { get; set; } = true;

        public bool CanPush(MessagingClient client, HorseMessage message) => AllowPush;
        public bool CanSubscribe(HorseChannel channel, MessagingClient client) => AllowSubscribe;
    }

    private class RecordingEventHandler : IChannelEventHandler
    {
        public int Created;
        public int Removed;
        public int Published;
        public int Subscribed;
        public int Unsubscribed;

        public Task OnCreated(HorseChannel channel) => Increment(ref Created);
        public Task OnRemoved(HorseChannel channel) => Increment(ref Removed);
        public Task OnPublish(HorseChannel channel, HorseMessage message) => Increment(ref Published);
        public Task OnSubscribe(HorseChannel channel, MessagingClient client) => Increment(ref Subscribed);
        public Task OnUnsubscribe(HorseChannel channel, MessagingClient client) => Increment(ref Unsubscribed);

        private static Task Increment(ref int value)
        {
            Interlocked.Increment(ref value);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ClientCreatesChannelWithOptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient client = await ChannelTestClient.Connect(port);
            HorseResult result = await client.Client.Channel.Create("with-options", o =>
            {
                o.ClientLimit = 5;
                o.MessageSizeLimit = 1024;
                o.AutoDestroy = false;
                o.SendLastMessageAsInitial = true;
                o.AutoDestroyIdleSeconds = 120;
            }, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);

            HorseChannel channel = server.Rider.Channel.Find("with-options");
            Assert.NotNull(channel);
            Assert.Equal(5, channel.Options.ClientLimit);
            Assert.Equal(1024UL, channel.Options.MessageSizeLimit);
            Assert.False(channel.Options.AutoDestroy);
            Assert.True(channel.Options.SendLastMessageAsInitial);
            Assert.Equal(120, channel.Options.AutoDestroyIdleSeconds);
        });
    }

    [Fact]
    public async Task CreateOnExistingChannelUpdatesOptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("update", o => o.ClientLimit = 2);

            ChannelTestClient client = await ChannelTestClient.Connect(port);
            HorseResult result = await client.Client.Channel.Create("update", o => o.ClientLimit = 10, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.Same(channel, server.Rider.Channel.Find("update"));
            Assert.Equal(10, channel.Options.ClientLimit);
        });
    }

    [Fact]
    public async Task CreatingDuplicateChannelThrows()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            await server.Rider.Channel.Create("duplicate");
            await Assert.ThrowsAsync<DuplicateNameException>(() => server.Rider.Channel.Create("duplicate"));
        });
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has@at")]
    [InlineData("has;semicolon")]
    [InlineData("has*star")]
    public async Task CreatingChannelWithInvalidNameThrows(string name)
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => server.Rider.Channel.Create(name));
            Assert.Null(server.Rider.Channel.Find(name));
        });
    }

    [Fact]
    public async Task SubscribeAndPublishCreateChannelWhenAutoCreationEnabled()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient client = await ChannelTestClient.Connect(port);

            Assert.Equal(HorseResultCode.Ok, (await client.Client.Channel.Subscribe("auto-sub", true, CancellationToken.None)).Code);
            Assert.NotNull(server.Rider.Channel.Find("auto-sub"));

            Assert.Equal(HorseResultCode.Ok, (await client.Client.Channel.PublishString("auto-pub", "x", true, CancellationToken.None)).Code);
            Assert.NotNull(server.Rider.Channel.Find("auto-pub"));
        });
    }

    [Fact]
    public async Task SubscribeAndPublishReturnNotFoundWhenAutoCreationDisabled()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Channel.Options.AutoChannelCreation = false;
            ChannelTestClient client = await ChannelTestClient.Connect(port);

            HorseResult subscribe = await client.Client.Channel.Subscribe("missing-sub", true, CancellationToken.None);
            HorseResult publish = await client.Client.Channel.PublishString("missing-pub", "x", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.NotFound, subscribe.Code);
            Assert.Equal(HorseResultCode.NotFound, publish.Code);
            Assert.Null(server.Rider.Channel.Find("missing-sub"));
            Assert.Null(server.Rider.Channel.Find("missing-pub"));
        });
    }

    [Fact]
    public async Task ClientDeleteRemovesChannelAndSubscriptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("to-delete", true, CancellationToken.None);
            HorseChannel channel = server.Rider.Channel.Find("to-delete");

            ChannelTestClient admin = await ChannelTestClient.Connect(port);
            Assert.Equal(HorseResultCode.Ok, (await admin.Client.Channel.Delete("to-delete", true, CancellationToken.None)).Code);

            Assert.Null(server.Rider.Channel.Find("to-delete"));
            Assert.Equal(ChannelStatus.Destroyed, channel.Status);
            Assert.Empty(server.Rider.Client.Find(subscriber.Client.ClientId).GetChannels());
        });
    }

    [Fact]
    public async Task ListReturnsChannelsMatchingFilter()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Channel.Create("list-a");
            await server.Rider.Channel.Create("list-b");
            await server.Rider.Channel.Create("other");

            ChannelTestClient client = await ChannelTestClient.Connect(port);

            var all = await client.Client.Channel.List(CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, all.Result.Code);
            Assert.Equal(new[] { "list-a", "list-b", "other" }, all.Model.Select(x => x.Name).OrderBy(x => x));

            var filtered = await client.Client.Channel.List("list-*", CancellationToken.None);
            Assert.Equal(new[] { "list-a", "list-b" }, filtered.Model.Select(x => x.Name).OrderBy(x => x));
        });
    }

    [Fact]
    public async Task GetSubscribersReturnsSubscribedClients()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient first = await ChannelTestClient.Connect(port);
            ChannelTestClient second = await ChannelTestClient.Connect(port);
            await first.Client.Channel.Subscribe("subscribers", true, CancellationToken.None);
            await second.Client.Channel.Subscribe("subscribers", true, CancellationToken.None);

            var result = await first.Client.Channel.GetSubscribers("subscribers", CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Result.Code);
            Assert.Equal(new[] { first.Client.ClientId, second.Client.ClientId }.OrderBy(x => x),
                result.Model.Select(x => x.Id).OrderBy(x => x));
        });
    }

    [Fact]
    public async Task UnauthorizedPushIsRejectedAndNotDelivered()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelAuthorization authorization = new ChannelAuthorization { AllowPush = false };
            server.Rider.Channel.Authenticators.Add(authorization);
            await server.Rider.Channel.Create("secured-push");

            ChannelTestClient subscriber = await ChannelTestClient.Connect(port);
            await subscriber.Client.Channel.Subscribe("secured-push", true, CancellationToken.None);

            ChannelTestClient publisher = await ChannelTestClient.Connect(port);
            HorseResult result = await publisher.Client.Channel.PublishString("secured-push", "x", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Unauthorized, result.Code);
            await Task.Delay(200);
            Assert.Empty(subscriber.Received);
        });
    }

    [Fact]
    public async Task UnauthorizedSubscribeIsRejected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Channel.Authenticators.Add(new ChannelAuthorization { AllowSubscribe = false });
            HorseChannel existing = await server.Rider.Channel.Create("secured-sub");

            ChannelTestClient client = await ChannelTestClient.Connect(port);

            Assert.Equal(HorseResultCode.Unauthorized, (await client.Client.Channel.Subscribe("secured-sub", true, CancellationToken.None)).Code);
            Assert.Equal(0, existing.ClientsCount());

            //auto created channel checks the authorization too
            Assert.Equal(HorseResultCode.Unauthorized, (await client.Client.Channel.Subscribe("secured-auto", true, CancellationToken.None)).Code);
            Assert.Equal(0, server.Rider.Channel.Find("secured-auto")?.ClientsCount() ?? 0);
        });
    }

    [Fact]
    public async Task EventHandlersAreCalledThroughChannelLifecycle()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingEventHandler handler = new RecordingEventHandler();
            server.Rider.Channel.EventHandlers.Add(handler);

            ChannelTestClient client = await ChannelTestClient.Connect(port);
            await client.Client.Channel.Subscribe("lifecycle", true, CancellationToken.None);
            await client.Client.Channel.PublishString("lifecycle", "x", true, CancellationToken.None);
            await client.Client.Channel.Unsubscribe("lifecycle", true, CancellationToken.None);
            await client.Client.Channel.Delete("lifecycle", true, CancellationToken.None);

            await TestHorseRider.WaitUntil(() => handler.Removed == 1 && handler.Published == 1);

            Assert.Equal(1, handler.Created);
            Assert.Equal(1, handler.Subscribed);
            Assert.Equal(1, handler.Published);
            Assert.Equal(1, handler.Unsubscribed);
            Assert.Equal(1, handler.Removed);
        });
    }

    [Fact]
    public async Task GetSubscribersResponseHasSubscribersContentType()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ChannelTestClient client = await ChannelTestClient.Connect(port);
            await client.Client.Channel.Subscribe("subscribers-type", true, CancellationToken.None);

            var result = await client.Client.Channel.GetSubscribers("subscribers-type", CancellationToken.None);

            Assert.NotNull(result.Result.Message);
            Assert.Equal(KnownContentTypes.ChannelSubscribers, result.Result.Message.ContentType);
        });
    }

    [Fact]
    public async Task InfoFollowsTopicAndStatusChanges()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseChannel channel = await server.Rider.Channel.Create("info");
            Assert.Equal(ChannelStatus.Running.ToString(), channel.Info.Status);

            channel.Topic = "sports";
            channel.Status = ChannelStatus.Paused;

            Assert.Equal("sports", channel.Info.Topic);
            Assert.Equal(ChannelStatus.Paused.ToString(), channel.Info.Status);

            ChannelTestClient client = await ChannelTestClient.Connect(port);
            var list = await client.Client.Channel.List("info", CancellationToken.None);
            ChannelInformation info = list.Model.Single();
            Assert.Equal("sports", info.Topic);
            Assert.Equal(ChannelStatus.Paused.ToString(), info.Status);

            server.Rider.Channel.Remove(channel);
            Assert.Equal(ChannelStatus.Destroyed.ToString(), channel.Info.Status);
        });
    }
}
