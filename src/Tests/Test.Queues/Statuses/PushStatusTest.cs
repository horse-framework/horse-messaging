using System;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Queues.Statuses
{
    /// <summary>
    /// Ports 47200 - 47220
    /// </summary>
    public class PushStatusTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public async Task SendToOnlineConsumers(int onlineConsumerCount)
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int msgReceived = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                TmqClient consumer = new TmqClient();
                consumer.ClientId = "consumer-" + i;
                await consumer.ConnectAsync("tmq://localhost:" + port);
                Assert.True(consumer.IsConnected);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
                TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
                Assert.Equal(TwinoResultCode.Ok, joined.Code);
            }

            await producer.Queues.Push("push-a", "Hello, World!", false);
            await Task.Delay(1500);
            Assert.Equal(onlineConsumerCount, msgReceived);
        }

        [Fact]
        public async Task SendToOfflineConsumers()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "Hello, World!", false);
            await Task.Delay(700);

            TwinoQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            bool msgReceived = false;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(800);
            Assert.True(msgReceived);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RequestAcknowledge(bool queueAckIsActive)
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start(300, 300);
            TwinoQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            queue.Options.Acknowledge = queueAckIsActive ? QueueAckDecision.JustRequest : QueueAckDecision.None;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient consumer = new TmqClient();
            consumer.AutoAcknowledge = true;
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TwinoResult ack = await producer.Queues.Push("push-a", "Hello, World!", true);
            Assert.Equal(queueAckIsActive, ack.Code == TwinoResultCode.Ok);
        }

        /// <summary>
        /// Pushes message to multiple channels with CC header
        /// </summary>
        [Fact]
        public async Task PushWithCC()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient consumer1 = new TmqClient();
            consumer1.ClientId = "consumer-1";
            await consumer1.ConnectAsync("tmq://localhost:" + port);

            TmqClient consumer2 = new TmqClient();
            consumer2.ClientId = "consumer-2";
            await consumer2.ConnectAsync("tmq://localhost:" + port);

            Assert.True(consumer1.IsConnected);
            Assert.True(consumer2.IsConnected);

            int consumer1Msgs = 0;
            int consumer2Msgs = 0;
            consumer1.MessageReceived += (c, m) => consumer1Msgs++;
            consumer2.MessageReceived += (c, m) => consumer2Msgs++;

            TwinoResult joined1 = await consumer1.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined1.Code);

            TwinoResult joined2 = await consumer2.Queues.Subscribe("push-a-cc", true);
            Assert.Equal(TwinoResultCode.Ok, joined2.Code);

            TwinoMessage msg = new TwinoMessage(MessageType.QueueMessage, "push-a", MessageA.ContentType);
            msg.AddHeader(TwinoHeaders.CC, "push-a-cc");
            msg.SetStringContent("Hello, World!");

            await producer.SendAsync(msg);
            await Task.Delay(1500);

            Assert.Equal(1, consumer1Msgs);
            Assert.Equal(1, consumer2Msgs);
        }
    }
}