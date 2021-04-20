using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Test.Common;
using Xunit;

namespace Test.Queues.Types
{
    public class PullTypeTest
    {
        [Fact]
        public async Task SendAndPull()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queue.Subscribe("pull-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queue.Push("pull-a", "Hello, World!", false);
            await Task.Delay(700);

            HorseQueue queue = server.Rider.Queue.Find("pull-a");
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            PullRequest request = new PullRequest();
            request.Queue = "pull-a";
            request.Count = 1;
            request.ClearAfter = ClearDecision.None;
            request.GetQueueMessageCounts = false;
            request.Order = MessageOrder.FIFO;

            PullContainer container1 = await consumer.Queue.Pull(request);
            Assert.Equal(PullProcess.Completed, container1.Status);
            Assert.NotEmpty(container1.ReceivedMessages);

            PullContainer container2 = await consumer.Queue.Pull(request);
            Assert.Equal(PullProcess.Empty, container2.Status);
            Assert.Empty(container2.ReceivedMessages);
        }

        [Fact]
        public async Task RequestAcknowledge()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("pull-a");
            Assert.NotNull(queue);
            queue.Options.Acknowledge = QueueAckDecision.JustRequest;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);

            HorseClient consumer = new HorseClient();
            consumer.AutoAcknowledge = true;
            consumer.ClientId = "consumer";

            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);

            bool msgReceived = false;
            consumer.MessageReceived += (c, m) => msgReceived = true;
            HorseResult joined = await consumer.Queue.Subscribe("pull-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseClient producer = new HorseClient();
            producer.ResponseTimeout = TimeSpan.FromSeconds(15);
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            Task<HorseResult> taskAck = producer.Queue.Push("pull-a", "Hello, World!", true);

            await Task.Delay(500);
            Assert.False(taskAck.IsCompleted);
            Assert.False(msgReceived);
            Assert.Single(queue.Messages);

            consumer.PullTimeout = TimeSpan.FromDays(1);

            PullContainer pull = await consumer.Queue.Pull(PullRequest.Single("pull-a"));
            Assert.Equal(PullProcess.Completed, pull.Status);
            Assert.Equal(1, pull.ReceivedCount);
            Assert.NotEmpty(pull.ReceivedMessages);
        }

        /// <summary>
        /// Pull messages in FIFO and LIFO order
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PullOrder(bool? fifo)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("pull-a");
            await queue.Push("First Message");
            await queue.Push("Second Message");

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            HorseResult joined = await client.Queue.Subscribe("pull-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            PullRequest request = new PullRequest
            {
                Queue = "pull-a",
                Count = 1,
                Order = !fifo.HasValue || fifo.Value ? MessageOrder.FIFO : MessageOrder.LIFO
            };

            PullContainer container = await client.Queue.Pull(request);
            Assert.Equal(PullProcess.Completed, container.Status);

            HorseMessage msg = container.ReceivedMessages.FirstOrDefault();
            Assert.NotNull(msg);

            string content = msg.GetStringContent();
            if (fifo.HasValue && !fifo.Value)
                Assert.Equal("Second Message", content);
            else
                Assert.Equal("First Message", content);
        }

        /// <summary>
        /// Pull multiple messages in a request
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task PullCount(int count)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("pull-a");
            for (int i = 0; i < 25; i++)
                await queue.Push("Hello, World");

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            HorseResult joined = await client.Queue.Subscribe("pull-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            PullRequest request = new PullRequest
            {
                Queue = "pull-a",
                Count = count
            };

            PullContainer container = await client.Queue.Pull(request);
            Assert.Equal(count, container.ReceivedCount);
            Assert.Equal(PullProcess.Completed, container.Status);
        }

        /// <summary>
        /// Clear messages after pull operation is completed
        /// </summary>
        [Theory]
        [InlineData(2, true, true)]
        [InlineData(3, true, false)]
        [InlineData(4, false, true)]
        public async Task PullClearAfter(int count, bool priorityMessages, bool messages)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start();

            HorseQueue queue = server.Rider.Queue.Find("pull-a");
            for (int i = 0; i < 5; i++)
            {
                await queue.Push("Hello, World");
                await queue.Push("Hello, World");
            }

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            HorseResult joined = await client.Queue.Subscribe("pull-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            ClearDecision clearDecision = ClearDecision.None;
            if (priorityMessages && messages)
                clearDecision = ClearDecision.AllMessages;
            else if (priorityMessages)
                clearDecision = ClearDecision.PriorityMessages;
            else if (messages)
                clearDecision = ClearDecision.Messages;

            PullRequest request = new PullRequest
            {
                Queue = "pull-a",
                Count = count,
                ClearAfter = clearDecision
            };

            PullContainer container = await client.Queue.Pull(request);
            Assert.Equal(count, container.ReceivedCount);

            Assert.Equal(PullProcess.Completed, container.Status);

            if (priorityMessages)
                Assert.Empty(queue.PriorityMessages);

            if (messages)
                Assert.Empty(queue.Messages);
        }
    }
}