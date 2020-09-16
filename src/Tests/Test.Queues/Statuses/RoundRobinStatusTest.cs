using System;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Queues.Statuses
{
    public class RoundRobinStatusTest
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
            int msgSent = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                TmqClient consumer = new TmqClient();
                consumer.ClientId = "consumer-" + i;
                await consumer.ConnectAsync("tmq://localhost:" + port);
                Assert.True(consumer.IsConnected);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
                TwinoResult joined = await consumer.Queues.Subscribe("rr-a", true);
                Assert.Equal(TwinoResultCode.Ok, joined.Code);
            }

            for (int i = 0; i < 50; i++)
            {
                await producer.Queues.Push("rr-a", "Hello, World!", false);
                msgSent++;
            }

            await Task.Delay(1500);
            Assert.Equal(msgSent, msgReceived);
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

            await producer.Queues.Push("rr-a", "Hello, World!", false);
            await Task.Delay(700);

            TwinoQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            bool msgReceived = false;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            TwinoResult joined = await consumer.Queues.Subscribe("rr-a", true);
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

            TwinoQueue queue = server.Server.FindQueue("rr-a");
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
            TwinoResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TwinoResult ack = await producer.Queues.Push("rr-a", "Hello, World!", true);
            Assert.Equal(queueAckIsActive, ack.Code == TwinoResultCode.Ok);
        }
    }
}