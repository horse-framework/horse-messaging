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
                HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
                Assert.Equal(HorseResultCode.Ok, joined.Code);
            }

            await producer.Queues.Push("push-a", "Hello, World!", false);
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

            await producer.Queues.Push("push-a", "Hello, World!", false);
            await Task.Delay(700);

            HorseQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            bool msgReceived = false;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(800);
            Assert.True(msgReceived);
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

        /// <summary>
        /// Pushes message to multiple channels with CC header
        /// </summary>
        [Fact]
        public async Task PushWithCC()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            HorseClient consumer1 = new HorseClient();
            consumer1.ClientId = "consumer-1";
            await consumer1.ConnectAsync("horse://localhost:" + port);

            HorseClient consumer2 = new HorseClient();
            consumer2.ClientId = "consumer-2";
            await consumer2.ConnectAsync("horse://localhost:" + port);

            Assert.True(consumer1.IsConnected);
            Assert.True(consumer2.IsConnected);

            int consumer1Msgs = 0;
            int consumer2Msgs = 0;
            consumer1.MessageReceived += (c, m) => { consumer1Msgs++; };
            consumer2.MessageReceived += (c, m) => { consumer2Msgs++; };

            HorseResult joined1 = await consumer1.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined1.Code);

            HorseResult joined2 = await consumer2.Queues.Subscribe("push-a-cc", true);
            Assert.Equal(HorseResultCode.Ok, joined2.Code);

            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "push-a");
            msg.AddHeader(HorseHeaders.CC, "push-a-cc");
            msg.SetStringContent("Hello, World!");

            await producer.SendAsync(msg);
            await Task.Delay(1500);

            Assert.Equal(1, consumer1Msgs);
            Assert.Equal(1, consumer2Msgs);
        }
    }
}