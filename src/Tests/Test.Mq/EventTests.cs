using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    /// <summary>
    /// Ports 42250 - 42280
    /// </summary>
    public class EventTests
    {
        [Fact]
        public async Task ClientConnected()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42251);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42251");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientConnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:42251");
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientConnected();
            Assert.True(unsubscribed);
            client2.Disconnect();
            await client2.ConnectAsync("tmq://localhost:42251");
            Assert.True(client2.IsConnected);
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientDisconnected()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42252);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42252");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientDisconnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:42252");
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientDisconnected();
            Assert.True(unsubscribed);

            await client2.ConnectAsync("tmq://localhost:42252");
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientJoined()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42253);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42253");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Channels.OnClientJoined("ch-push", c =>
            {
                Assert.Equal("ch-push", c.Channel);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Channels.Join("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Channels.OffClientJoined("ch-push");
            Assert.True(unsubscribed);

            result = await client.Channels.Leave("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            result = await client.Channels.Join("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
        }

        [Fact]
        public async Task ClientLeft()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42254);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42254");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Channels.OnClientLeft("ch-push", c =>
            {
                Assert.Equal("ch-push", c.Channel);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Channels.Join("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
            result = await client.Channels.Leave("ch-push", true);
            await Task.Delay(500);
            Assert.True(received);

            // ReSharper disable once HeuristicUnreachableCode
            received = false;

            bool unsubscribed = await client.Channels.OffClientLeft("ch-push");
            Assert.True(unsubscribed);

            Assert.Equal(TwinoResultCode.Ok, result.Code);
            result = await client.Channels.Join("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            result = await client.Channels.Leave("ch-push", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
        }

        [Fact]
        public async Task ChannelCreated()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42255);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42255");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Channels.OnCreated(c =>
            {
                Assert.Equal("created-channel", c.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Channels.Create("created-channel");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Channels.OffCreated();
            Assert.True(unsubscribed);

            result = await client.Channels.Create("created-channel-2");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
        }

        [Fact]
        public async Task ChannelRemoved()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42256);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42256");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Channels.OnRemoved(c =>
            {
                Assert.Equal("ch-push", c.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Channels.Remove("ch-push");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Channels.OffRemoved();
            Assert.True(unsubscribed);

            result = await client.Channels.Remove("ch-pull");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
        }

        [Fact]
        public async Task QueueCreated()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42257);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42257");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnCreated("ch-pull", c =>
            {
                Assert.Equal("ch-pull", c.Channel);
                Assert.Equal(1002, c.Id);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Create("ch-pull", 1002);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queues.OffCreated("ch-pull");
            Assert.True(unsubscribed);

            result = await client.Queues.Create("ch-pull", 1200);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.False(received);
        }

        [Fact]
        public async Task QueueUpdated()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42258);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42258");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnUpdated("ch-pull", c =>
            {
                Assert.Equal("ch-pull", c.Channel);
                Assert.Equal(1001, c.Id);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.SetOptions("ch-pull", 1001, opt =>
            {
                opt.MessageLimit = 666;
            });
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
        }

        [Fact]
        public async Task QueueRemoved()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42259);
            server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42259");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnRemoved("ch-pull", c =>
            {
                Assert.Equal("ch-pull", c.Channel);
                Assert.Equal(1001, c.Id);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Remove("ch-pull", 1001);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
        }

        [Fact]
        public async Task MessageProduced()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42260);
            server.Start(3000, 3000);
            server.SendAcknowledgeFromMQ = true;
            
            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42260");
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnMessageProduced("ch-pull", 1001, c =>
            {
                Assert.Equal("ch-pull", c.Channel);
                Assert.Equal(1001, c.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Push("ch-pull", 1001, "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);
            Assert.True(received);
        }
    }
}