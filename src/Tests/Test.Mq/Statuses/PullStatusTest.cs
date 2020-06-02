using System;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Models;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Statuses
{
    public class PullStatusTest
    {
        [Fact]
        public async Task SendAndPull()
        {
            int port = 47411;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            TwinoResult joined = await consumer.Channels.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("ch-pull", MessageA.ContentType, "Hello, World!", false);
            await Task.Delay(700);

            Channel channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(channel);
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            PullRequest request = new PullRequest();
            request.Channel = "ch-pull";
            request.QueueId = MessageA.ContentType;
            request.Count = 1;
            request.ClearAfter = ClearDecision.None;
            request.GetQueueMessageCounts = false;
            request.Order = MessageOrder.FIFO;

            PullContainer container1 = await consumer.Queues.Pull(request);
            Assert.Equal(PullProcess.Completed, container1.Status);
            Assert.NotEmpty(container1.ReceivedMessages);

            PullContainer container2 = await consumer.Queues.Pull(request);
            Assert.Equal(PullProcess.Empty, container2.Status);
            Assert.Empty(container2.ReceivedMessages);
        }

        [Fact]
        public async Task RequestAcknowledge()
        {
            int port = 47412;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Channel channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(channel);
            Assert.NotNull(queue);
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);

            TmqClient consumer = new TmqClient();
            consumer.AutoAcknowledge = true;
            consumer.ClientId = "consumer";

            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);

            bool msgReceived = false;
            consumer.MessageReceived += (c, m) => msgReceived = true;
            TwinoResult joined = await consumer.Channels.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TmqClient producer = new TmqClient();
            producer.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            Task<TwinoResult> taskAck = producer.Queues.Push("ch-pull", MessageA.ContentType, "Hello, World!", true);

            await Task.Delay(500);
            Assert.False(taskAck.IsCompleted);
            Assert.False(msgReceived);
            Assert.Single(queue.Messages);

            consumer.PullTimeout = TimeSpan.FromDays(1);

            PullContainer pull = await consumer.Queues.Pull(PullRequest.Single("ch-pull", MessageA.ContentType));
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
            int port = 47413;
            if (fifo.HasValue)
                port += fifo.Value ? 1 : 2;
            
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            var channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            queue.AddStringMessageWithId("First Message");
            queue.AddStringMessageWithId("Second Message");

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            TwinoResult joined = await client.Channels.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            PullRequest request = new PullRequest
                                  {
                                      Channel = "ch-pull",
                                      QueueId = MessageA.ContentType,
                                      Count = 1,
                                      Order = !fifo.HasValue || fifo.Value ? MessageOrder.FIFO : MessageOrder.LIFO
                                  };

            PullContainer container = await client.Queues.Pull(request);
            Assert.Equal(PullProcess.Completed, container.Status);

            TmqMessage msg = container.ReceivedMessages.FirstOrDefault();
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
            int port = 47421 + count;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            var channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            for (int i = 0; i < 25; i++)
                queue.AddStringMessageWithId("Hello, World");

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            TwinoResult joined = await client.Channels.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            PullRequest request = new PullRequest
                                  {
                                      Channel = "ch-pull",
                                      QueueId = MessageA.ContentType,
                                      Count = count
                                  };

            PullContainer container = await client.Queues.Pull(request);
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
            int port = 47489 + count;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            var channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            for (int i = 0; i < 5; i++)
            {
                queue.AddStringMessageWithId("Hello, World");
                queue.AddStringMessageWithId("Hello, World", false, true);
            }

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            TwinoResult joined = await client.Channels.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            ClearDecision clearDecision = ClearDecision.None;
            if (priorityMessages && messages)
                clearDecision = ClearDecision.AllMessages;
            else if (priorityMessages)
                clearDecision = ClearDecision.PriorityMessages;
            else if (messages)
                clearDecision = ClearDecision.Messages;

            PullRequest request = new PullRequest
                                  {
                                      Channel = "ch-pull",
                                      QueueId = MessageA.ContentType,
                                      Count = count,
                                      ClearAfter = clearDecision
                                  };

            PullContainer container = await client.Queues.Pull(request);
            Assert.Equal(count, container.ReceivedCount);

            Assert.Equal(PullProcess.Completed, container.Status);

            if (priorityMessages)
                Assert.Empty(queue.PriorityMessages);

            if (messages)
                Assert.Empty(queue.Messages);
        }
    }
}