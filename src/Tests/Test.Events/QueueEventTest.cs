using System.Threading.Tasks;
using Test.Common;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Events
{
    public class QueueEventTest
    {
        [Fact]
        public async Task QueueCreated()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnCreated(q =>
            {
                Assert.Equal("pull-b", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Create("pull-b");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queues.OffCreated();
            Assert.True(unsubscribed);

            result = await client.Queues.Create("pull-c");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }

        [Fact]
        public async Task QueueUpdated()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnUpdated(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.SetOptions("pull-a", opt => { opt.MessageLimit = 666; });
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task QueueRemoved()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnRemoved(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Remove("pull-a");
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task MessageProduced()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(3000, 3000);
            server.SendAcknowledgeFromMQ = true;

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queues.OnMessageProduced("pull-a",q =>
            {
                Assert.Equal("pull-a", q.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queues.Push("pull-a", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }
    }
}