using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Test.Common;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
using Xunit;

namespace Test.Queues.Statuses
{
    public class BroadcastStatusTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public async Task SendToOnlineConsumers(int onlineConsumerCount)
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            int msgReceived = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                HorseClient consumer = new HorseClient();
                consumer.ClientId = "consumer-" + i;
                await consumer.ConnectAsync("horse://localhost:" + port);
                Assert.True(consumer.IsConnected);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
                HorseResult joined = await consumer.Queues.Subscribe("broadcast-a", true);
                Assert.Equal(HorseResultCode.Ok, joined.Code);
            }

            await producer.Queues.Push("broadcast-a", "Hello, World!", false);
            await Task.Delay(1500);
            Assert.Equal(onlineConsumerCount, msgReceived);
        }

        [Fact]
        public async Task SendToOfflineConsumers()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("broadcast-a", "Hello, World!", false);
            await Task.Delay(700);

            bool msgReceived = false;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            HorseResult joined = await consumer.Queues.Subscribe("broadcast-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(800);
            Assert.False(msgReceived);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RequestAcknowledge(bool queueAckIsActive)
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);
            HorseQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            queue.Options.Acknowledge = queueAckIsActive ? QueueAckDecision.JustRequest : QueueAckDecision.None;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            HorseClient consumer = new HorseClient();
            consumer.AutoAcknowledge = true;
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseResult ack = await producer.Queues.Push("push-a", "Hello, World!", true);
            Assert.Equal(queueAckIsActive, ack.Code == HorseResultCode.Ok);
        }
    }
}