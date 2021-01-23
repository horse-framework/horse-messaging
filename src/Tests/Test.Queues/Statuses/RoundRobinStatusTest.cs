using System;
using System.Threading;
using System.Threading.Tasks;
using Test.Common;
using Horse.Mq.Client;
using Horse.Mq.Delivery;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);
            server.Server.Options.Acknowledge = QueueAckDecision.None;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int msgReceived = 0;
            int msgSent = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                HorseClient consumer = new HorseClient();
                consumer.ClientId = "consumer-" + i;
                await consumer.ConnectAsync("hmq://localhost:" + port);
                Assert.True(consumer.IsConnected);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
                HorseResult joined = await consumer.Queues.Subscribe("rr-a", true);
                Assert.Equal(HorseResultCode.Ok, joined.Code);
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
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("rr-a", "Hello, World!", false);
            await Task.Delay(700);

            HorseQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            Assert.Single(queue.Messages);

            bool msgReceived = false;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            HorseResult joined = await consumer.Queues.Subscribe("rr-a", true);
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

            HorseQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            queue.Options.Acknowledge = queueAckIsActive ? QueueAckDecision.JustRequest : QueueAckDecision.None;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            HorseClient consumer = new HorseClient();
            consumer.AutoAcknowledge = true;
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseResult ack = await producer.Queues.Push("rr-a", "Hello, World!", true);
            Assert.Equal(queueAckIsActive, ack.Code == HorseResultCode.Ok);
        }

        [Fact]
        public async Task WaitForAcknowledge()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int received = 0;
            HorseClient consumer = new HorseClient();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            consumer.MessageReceived += async (client, message) =>
            {
                received++;
                await Task.Delay(100);
                await consumer.SendAck(message);
            };

            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            for (int i = 0; i < 50; i++)
                await producer.Queues.Push("rr-a", "Hello, World!", false);

            await Task.Delay(1050);
            Assert.True(received > 8);
            Assert.True(received < 12);
        }

        [Fact]
        public async Task AcknowledgeTimeout()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.End;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int received = 0;
            HorseClient consumer = new HorseClient();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            consumer.MessageReceived += async (client, message) => received++;

            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queues.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            for (int i = 0; i < 10; i++)
                await producer.Queues.Push("rr-a", "Hello, World!", false);

            await Task.Delay(3500);
            Assert.Equal(2, received);
            
            //1 msg is pending ack, 1 msg is prepared and ready to send (waiting for ack) and 8 msgs are in queue
            Assert.Equal(8, queue.MessageCount());
        }

        [Fact]
        public async Task MultithreadConsumers()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.End;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int c1 = 0;
            int c2 = 0;
            int c3 = 0;
            
            HorseClient consumer1 = new HorseClient();
            consumer1.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer1.ClientId = "consumer1";
            consumer1.MessageReceived += async (client, message) => c1++;
            
            HorseClient consumer2 = new HorseClient();
            consumer2.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer2.ClientId = "consumer2";
            consumer2.MessageReceived += async (client, message) => c2++;
            
            HorseClient consumer3 = new HorseClient();
            consumer3.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer3.ClientId = "consumer3";
            consumer3.MessageReceived += async (client, message) => c3++;

            await consumer1.ConnectAsync("hmq://localhost:" + port);
            await consumer2.ConnectAsync("hmq://localhost:" + port);
            await consumer3.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer1.IsConnected);
            Assert.True(consumer2.IsConnected);
            Assert.True(consumer3.IsConnected);
            
            HorseResult joined1 = await consumer1.Queues.Subscribe("rr-a", true);
            HorseResult joined2 = await consumer2.Queues.Subscribe("rr-a", true);
            HorseResult joined3 = await consumer3.Queues.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined1.Code);
            Assert.Equal(HorseResultCode.Ok, joined2.Code);
            Assert.Equal(HorseResultCode.Ok, joined3.Code);

            for (int i = 0; i < 10; i++)
                await producer.Queues.Push("rr-a", "Hello, World!", false);
            
            await Task.Delay(1200);
            Assert.Equal(1, c1);
            Assert.Equal(1, c2);
            Assert.Equal(1, c3);
        }
    }
}