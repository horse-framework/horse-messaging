using System.Threading.Tasks;
using Test.Common;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;
using Xunit;

namespace Test.Events
{
    public class ClientEventTest
    {
        [Fact]
        public async Task ClientConnected()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientConnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            HorseClient client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientConnected();
            Assert.True(unsubscribed);
            client2.Disconnect();
            client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client2.IsConnected);
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientDisconnected()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connections.OnClientDisconnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            HorseClient client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connections.OffClientDisconnected();
            Assert.True(unsubscribed);

            await client2.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientSubscribed()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnSubscribed("push-a", c =>
            {
                Assert.Equal("push-a", c.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queues.OffSubscribed("push-a");
            Assert.True(unsubscribed);

            result = await client.Queues.Unsubscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }

        [Fact]
        public async Task ClientUnsubscribed()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("hmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnUnsubscribed("push-a", c =>
            {
                Assert.Equal("push-a", c.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
            result = await client.Queues.Unsubscribe("push-a", true);
            await Task.Delay(250);
            Assert.True(received);

            // ReSharper disable once HeuristicUnreachableCode
            received = false;

            bool unsubscribed = await client.Queues.OffUnsubscribed("push-a");
            Assert.True(unsubscribed);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queues.Unsubscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }
    }
}