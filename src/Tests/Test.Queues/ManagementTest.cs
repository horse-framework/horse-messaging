using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Horse.Messaging.Server.Client.Models;
using Test.Common;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult joined = await client.Queues.Subscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseQueue queue = server.Server.Queues.FirstOrDefault();
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult joined = await client.Queues.Subscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseResult left = await client.Queues.Unsubscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, left.Code);

            HorseQueue queue = server.Server.Queues.FirstOrDefault();
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult created = await client.Queues.Create("queue-new");
            Assert.Equal(HorseResultCode.Ok, created.Code);

            HorseQueue queue = server.Server.Queues.FirstOrDefault(x => x.Name == "queue-new");
            Assert.NotNull(queue);
            Assert.Equal("queue-new", queue.Name);
        }

        [Fact]
        public async Task CreateWithProperties()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult created = await client.Queues.Create("queue-test", o =>
            {
                o.AcknowledgeTimeout = 33000;
                o.Status = MessagingQueueStatus.Pull;
            });
            Assert.Equal(HorseResultCode.Ok, created.Code);

            HorseQueue queue = server.Server.FindQueue("queue-test");
            Assert.NotNull(queue);

            Assert.Equal(TimeSpan.FromSeconds(33), queue.Options.AcknowledgeTimeout);
            Assert.Equal(QueueStatus.Pull, queue.Status);
        }

        [Fact]
        public async Task Update()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Server.FindQueue("broadcast-a");
            Assert.NotNull(queue);

            Assert.Equal(TimeSpan.FromSeconds(12), queue.Options.MessageTimeout);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult updated = await client.Queues.SetOptions("broadcast-a", o => o.MessageTimeout = 666000);
            Assert.Equal(HorseResultCode.Ok, updated.Code);

            Assert.Equal(TimeSpan.FromSeconds(666), queue.Options.MessageTimeout);
        }

        [Fact]
        public async Task Delete()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Server.FindQueue("broadcast-a");
            Assert.NotNull(queue);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult done = await client.Queues.Remove("broadcast-a");
            Assert.Equal(HorseResultCode.Ok, done.Code);

            queue = server.Server.FindQueue("broadcast-a");
            Assert.Null(queue);
        }

        [Fact]
        public async Task GetQueueInfo()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var queues = await client.Queues.List("push-a");
            Assert.Equal(HorseResultCode.Ok, queues.Result.Code);
            Assert.NotNull(queues.Model);
            var pushQueue = queues.Model.FirstOrDefault(x => x.Name == "push-a");
            Assert.NotNull(pushQueue);
        }

        [Fact]
        public async Task GetQueueList()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var result = await client.Queues.List();
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Server.FindQueue("push-a");
            await queue.Push("Hello, World");
            await queue.Push("Hello, World", true);
            await Task.Delay(500);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            var result = await client.Queues.ClearMessages("push-a", priorityMessages, messages);
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