using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Test.Common;
using Test.Events.Handlers.Channel;
using Xunit;

namespace Test.Events
{
    public class ChannelEventTest
    {
        [Fact]
        public async Task CreateChannel()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ChannelCreateHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Channel.Create("channel", null, true);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, ChannelCreateHandler.Count);
            server.Stop();
        }

        [Fact]
        public async Task CreateRemove()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ChannelRemoveHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Channel.Create("channel", null, true);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            HorseResult removeResult = await client.Channel.Delete("channel", true);
            Assert.Equal(HorseResultCode.Ok, removeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, ChannelRemoveHandler.Count);
            server.Stop();
        }

        [Fact]
        public async Task CreateSubscribe()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ChannelSubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Channel.Subscribe("channel", true);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, ChannelSubscribeHandler.Count);
            server.Stop();
        }

        [Fact]
        public async Task CreateUnsubscribe()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ChannelUnsubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Channel.Subscribe("channel", true);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, ChannelUnsubscribeHandler.Count);

            HorseResult unsubscribeResult = await client.Channel.Unsubscribe("channel", true);
            Assert.Equal(HorseResultCode.Ok, unsubscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, ChannelUnsubscribeHandler.Count);
            server.Stop();
        }

        [Fact]
        public async Task CreatePublish()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ChannelPublishHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Channel.PublishString("channel", "Hello, World!", true);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, ChannelPublishHandler.Count);
            server.Stop();
        }
    }
}