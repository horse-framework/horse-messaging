using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Test.Common;
using Twino.Client.TMQ;
using Twino.MQ.Delivery;
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
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);
            server.Server.Options.Acknowledge = QueueAckDecision.None;

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

            await Task.Delay(500);
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
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
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
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
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

        [Fact]
        public async Task WaitForAcknowledge()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TwinoQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int received = 0;
            TmqClient consumer = new TmqClient();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            consumer.MessageReceived += async (client, message) =>
            {
                received++;
                await Task.Delay(100);
                await consumer.SendAck(message);
            };

            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            TwinoResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            for (int i = 0; i < 50; i++)
                await producer.Queues.Push("rr-a", "Hello, World!", false);

            await Task.Delay(1050);
            Assert.True(received > 8);
            Assert.True(received < 12);
        }

        [Fact]
        public async Task AcknowledgeTimeout()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TwinoQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.End;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int received = 0;
            TmqClient consumer = new TmqClient();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            consumer.MessageReceived += async (client, message) => received++;

            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            TwinoResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            for (int i = 0; i < 10; i++)
                await producer.Queues.Push("rr-a", "Hello, World!", false);

            await Task.Delay(3500);
            Assert.Equal(2, received);
            
            //1 msg is pending ack, 1 msg is prepared and ready to send (waiting for ack) and 8 msgs are in queue
            Assert.Equal(8, queue.MessageCount());
        }
    }
}