using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Events
{
    public class QueueEventTest
    {
        [Fact]
        public async Task QueueCreated()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnCreated(q =>
            {
                Assert.Equal("pull-b", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.Create("pull-b");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
            received = false;

            bool unsubscribed = await client.Queue.OffCreated();
            Assert.True(unsubscribed);

            result = await client.Queue.Create("pull-c");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.False(received);
        }

        [Fact]
        public async Task QueueUpdated()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnUpdated(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.SetOptions("pull-a", opt => { opt.MessageLimit = 666; });
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task QueueRemoved()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnRemoved(q =>
            {
                Assert.Equal("pull-a", q.Name);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.Remove("pull-a");
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }

        [Fact]
        public async Task MessageProduced()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(3000, 3000);
            server.SendAcknowledgeFromMQ = true;

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);
            bool received = false;
            bool subscribed = await client.Queue.OnMessageProduced("pull-a",q =>
            {
                Assert.Equal("pull-a", q.Queue);
                received = true;
            });
            Assert.True(subscribed);

            var result = await client.Queue.Push("pull-a", "Hello, World!", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(250);
            Assert.True(received);
        }
    }
}