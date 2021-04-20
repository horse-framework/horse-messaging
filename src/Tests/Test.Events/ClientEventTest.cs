using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Events
{
    public class ClientEventTest
    {
        [Fact]
        public async Task ClientConnected()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connection.OnClientConnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            HorseClient client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("horse://localhost:" + port);
            Assert.True(client2.IsConnected);
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connection.OffClientConnected();
            Assert.True(unsubscribed);
            client2.Disconnect();
            client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("horse://localhost:" + port);
            Assert.True(client2.IsConnected);
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientDisconnected()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Connection.OnClientDisconnected(c =>
            {
                if (c.Id == "client-2")
                    received = true;
            });
            Assert.True(subscribed);

            HorseClient client2 = new HorseClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("horse://localhost:" + port);
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Connection.OffClientDisconnected();
            Assert.True(unsubscribed);

            await client2.ConnectAsync("horse://localhost:" + port);
            Assert.True(client2.IsConnected);
            client2.Disconnect();
            await Task.Delay(500);

            Assert.False(received);
        }

        [Fact]
        public async Task ClientSubscribed()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnSubscribed("push-a", c =>
            {
                Assert.Equal("push-a", c.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queue.OffSubscribed("push-a");
            Assert.True(unsubscribed);

            result = await client.Queue.Unsubscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }

        [Fact]
        public async Task ClientUnsubscribed()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnUnsubscribed("push-a", c =>
            {
                Assert.Equal("push-a", c.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
            result = await client.Queue.Unsubscribe("push-a", true);
            await Task.Delay(250);
            Assert.True(received);

            // ReSharper disable once HeuristicUnreachableCode
            received = false;

            bool unsubscribed = await client.Queue.OffUnsubscribed("push-a");
            Assert.True(unsubscribed);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            result = await client.Queue.Unsubscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }
    }
}