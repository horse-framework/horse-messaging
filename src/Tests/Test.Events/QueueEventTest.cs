using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Test.Common;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
using Xunit;

namespace Test.Events
{
    public class QueueEventTest
    {
        [Fact]
        public async Task QueueCreated()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnCreated(q =>
            {
                Assert.Equal("pull-b", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Create("pull-b");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queues.OffCreated();
            Assert.True(unsubscribed);

            result = await client.Queues.Create("pull-c");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }

        [Fact]
        public async Task QueueUpdated()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnUpdated(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.SetOptions("pull-a", opt => { opt.MessageLimit = 666; });
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task QueueRemoved()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnRemoved(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Remove("pull-a");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task MessageProduced()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(3000, 3000);
            server.SendAcknowledgeFromMQ = true;

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnMessageProduced("pull-a",q =>
            {
                Assert.Equal("pull-a", q.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Push("pull-a", "Hello, World!", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }
    }
}