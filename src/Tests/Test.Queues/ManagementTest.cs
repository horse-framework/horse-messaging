using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Test.Common;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Xunit;

namespace Test.Queues
{
    public class ManagementTest
    {
        /// <summary>
        /// Client sends a queue subscription message to server
        /// </summary>
        [Fact]
        public async Task SubscribeToQueue()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult joined = await client.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseQueue queue = server.Rider.Queue.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            List<QueueClient> clients = queue.ClientsClone;
            Assert.Single(clients);
        }

        /// <summary>
        /// Client sends a queue unsubscription message to server
        /// </summary>
        [Fact]
        public async Task UnsubscribeToQueue()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult joined = await client.Queue.Subscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseResult left = await client.Queue.Unsubscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, left.Code);

            HorseQueue queue = server.Rider.Queue.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            List<QueueClient> clients = queue.ClientsClone;
            Assert.Empty(clients);
        }


        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public async Task Create()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult created = await client.Queue.Create("queue-new");
            Assert.Equal(HorseResultCode.Ok, created.Code);

            HorseQueue queue = server.Rider.Queue.Queues.FirstOrDefault(x => x.Name == "queue-new");
            Assert.NotNull(queue);
            Assert.Equal("queue-new", queue.Name);
        }

        [Fact]
        public async Task CreateWithProperties()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult created = await client.Queue.Create("queue-test", o =>
            {
                o.AcknowledgeTimeout = 33000;
                o.Type = MessagingQueueType.Pull;
            });
            Assert.Equal(HorseResultCode.Ok, created.Code);

            HorseQueue queue = server.Rider.Queue.Find("queue-test");
            Assert.NotNull(queue);

            Assert.Equal(TimeSpan.FromSeconds(33), queue.Options.AcknowledgeTimeout);
            Assert.Equal(QueueType.Pull, queue.Type);
        }

        [Fact]
        public async Task Update()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            Assert.Equal(TimeSpan.FromSeconds(12), queue.Options.MessageTimeout);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult updated = await client.Queue.SetOptions("push-a", o => o.MessageTimeout = 666000);
            Assert.Equal(HorseResultCode.Ok, updated.Code);

            Assert.Equal(TimeSpan.FromSeconds(666), queue.Options.MessageTimeout);
        }

        [Fact]
        public async Task Delete()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult done = await client.Queue.Remove("push-a");
            Assert.Equal(HorseResultCode.Ok, done.Code);

            queue = server.Rider.Queue.Find("push-a");
            Assert.Null(queue);
        }

        [Fact]
        public async Task GetQueueInfo()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var queues = await client.Queue.List("push-a");
            Assert.Equal(HorseResultCode.Ok, queues.Result.Code);
            Assert.NotNull(queues.Model);
            var pushQueue = queues.Model.FirstOrDefault(x => x.Name == "push-a");
            Assert.NotNull(pushQueue);
        }

        [Fact]
        public async Task GetQueueList()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var result = await client.Queue.List();
            Assert.Equal(HorseResultCode.Ok, result.Result.Code);
            Assert.NotNull(result.Model);
            var queue = result.Model.FirstOrDefault();
            Assert.NotNull(queue);
        }

        /// <summary>
        /// Clears messages in queue
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ClearMessages(bool priorityMessages, bool messages)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("push-a");
            await queue.Push("Hello, World");
            await queue.Push("Hello, World", true);
            await Task.Delay(500);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var result = await client.Queue.ClearMessages("push-a", priorityMessages, messages);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            if (priorityMessages)
                Assert.Empty(queue.PriorityMessages);
            else
                Assert.NotEmpty(queue.PriorityMessages);

            if (messages)
                Assert.Empty(queue.Messages);
            else
                Assert.NotEmpty(queue.Messages);
        }
    }
}