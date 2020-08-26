using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Statuses
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
                TwinoResult joined = await consumer.Channels.Join("ch-round", true);
                Assert.Equal(TwinoResultCode.Ok, joined.Code);
            }

            for (int i = 0; i < 50; i++)
            {
                await producer.Queues.Push("ch-round", MessageA.ContentType, "Hello, World!", false);
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

            await producer.Queues.Push("ch-round", MessageA.ContentType, "Hello, World!", false);
            await Task.Delay(700);

            Channel channel = server.Server.FindChannel("ch-round");
            TwinoQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(channel);
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            bool msgReceived = false;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            TwinoResult joined = await consumer.Channels.Join("ch-round", true);
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

            Channel ch = server.Server.FindChannel("ch-round");
            TwinoQueue queue = ch.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            queue.Options.RequestAcknowledge = queueAckIsActive;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient consumer = new TmqClient();
            consumer.AutoAcknowledge = true;
            consumer.AcknowledgeTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            TwinoResult joined = await consumer.Channels.Join("ch-round", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TwinoResult ack = await producer.Queues.Push("ch-round", MessageA.ContentType, "Hello, World!", true);
            Assert.Equal(queueAckIsActive, ack.Code == TwinoResultCode.Ok);
        }
    }
}