using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Models;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
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
            TwinoResult joined = await consumer.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Push("ch-pull", MessageA.ContentType, "Hello, World!", false);
            await Task.Delay(700);

            Channel channel = server.Server.FindChannel("ch-pull");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(channel);
            Assert.NotNull(queue);
            Assert.Single(queue.RegularMessages);

            PullRequest request = new PullRequest();
            request.Channel = "ch-pull";
            request.QueueId = MessageA.ContentType;
            request.Count = 1;
            request.ClearAfter = ClearDecision.None;
            request.GetQueueMessageCounts = false;
            request.Order = MessageOrder.FIFO;

            PullContainer container1 = await consumer.Pull(request);
            Assert.Equal(PullProcess.Completed, container1.Status);
            Assert.NotEmpty(container1.ReceivedMessages);

            PullContainer container2 = await consumer.Pull(request);
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
            TwinoResult joined = await consumer.Join("ch-pull", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TmqClient producer = new TmqClient();
            producer.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            Task<TwinoResult> taskAck = producer.Push("ch-pull", MessageA.ContentType, "Hello, World!", true);

            await Task.Delay(500);
            Assert.False(taskAck.IsCompleted);
            Assert.False(msgReceived);
            Assert.Single(queue.RegularMessages);

            consumer.PullTimeout = TimeSpan.FromDays(1);

            PullContainer pull = await consumer.Pull(PullRequest.Single("ch-pull", MessageA.ContentType));
            Assert.Equal(PullProcess.Completed, pull.Status);
            Assert.Equal(1, pull.ReceivedCount);
            Assert.NotEmpty(pull.ReceivedMessages);
        }
    }
}