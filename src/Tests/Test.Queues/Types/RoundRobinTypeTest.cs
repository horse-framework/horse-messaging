using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Common;
using Xunit;

namespace Test.Queues.Types
{
    public class RoundRobinTypeTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public async Task SendToOnlineConsumers(int onlineConsumerCount)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);
            server.Rider.Queue.Options.Acknowledge = QueueAckDecision.None;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            int msgReceived = 0;
            int msgSent = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                HorseClient consumer = new HorseClient();
                consumer.ClientId = "consumer-" + i;
                consumer.AutoAcknowledge = true;
                await consumer.ConnectAsync("horse://localhost:" + port);
                Assert.True(consumer.IsConnected);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
                HorseResult joined = await consumer.Queue.Subscribe("rr-a", true);
                Assert.Equal(HorseResultCode.Ok, joined.Code);
            }

            await Task.Delay(500);
            for (int i = 0; i < 50; i++)
            {
                await producer.Queue.Push("rr-a", "Hello, World!", false);
                msgSent++;
            }

            await Task.Delay(1500);
            Assert.Equal(msgSent, msgReceived);
            server.Stop();
        }

        [Fact]
        public async Task SendToOfflineConsumers()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queue.Push("rr-a", "Hello, World!", false);
            await Task.Delay(700);

            HorseQueue queue = server.Rider.Queue.Find("rr-a");
            Assert.NotNull(queue);
            Assert.Equal(1, queue.Manager.MessageStore.Count());

            bool msgReceived = false;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            HorseResult joined = await consumer.Queue.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(800);
            Assert.True(msgReceived);
            server.Stop();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RequestAcknowledge(bool queueAckIsActive)
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("rr-a");
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
            HorseResult joined = await consumer.Queue.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            HorseResult ack = await producer.Queue.Push("rr-a", "Hello, World!", true);
            Assert.True(ack.Code == HorseResultCode.Ok);
            server.Stop();
        }

        [Fact]
        public async Task WaitForAcknowledge()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            queue.Options.PutBack = PutBackDecision.Regular;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
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

            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queue.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            for (int i = 0; i < 50; i++)
                await producer.Queue.Push("rr-a", "Hello, World!", false);

            await Task.Delay(1050);
            Assert.True(received > 8);
            Assert.True(received < 12);
            server.Stop();
        }

        [Fact]
        public async Task AcknowledgeTimeout()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Regular;
            queue.Options.PutBack = PutBackDecision.Regular;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            int received = 0;
            HorseClient consumer = new HorseClient();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(4);
            consumer.ClientId = "consumer";
            consumer.MessageReceived += async (client, message) => received++;

            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            HorseResult joined = await consumer.Queue.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            for (int i = 0; i < 10; i++)
                await producer.Queue.Push("rr-a", "Hello, World!", false);

            await Task.Delay(3500);
            Assert.Equal(2, received);

            //1 msg is pending ack, 1 msg is prepared and ready to send (waiting for ack) and 8 msgs are in queue
            Assert.Equal(8, queue.Manager.MessageStore.Count());
            server.Stop();
        }

        [Fact]
        public async Task MultithreadConsumers()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("rr-a");
            Assert.NotNull(queue);
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Regular;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
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

            await consumer1.ConnectAsync("horse://localhost:" + port);
            await consumer2.ConnectAsync("horse://localhost:" + port);
            await consumer3.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer1.IsConnected);
            Assert.True(consumer2.IsConnected);
            Assert.True(consumer3.IsConnected);

            HorseResult joined1 = await consumer1.Queue.Subscribe("rr-a", true);
            HorseResult joined2 = await consumer2.Queue.Subscribe("rr-a", true);
            HorseResult joined3 = await consumer3.Queue.Subscribe("rr-a", true);
            Assert.Equal(HorseResultCode.Ok, joined1.Code);
            Assert.Equal(HorseResultCode.Ok, joined2.Code);
            Assert.Equal(HorseResultCode.Ok, joined3.Code);

            for (int i = 0; i < 10; i++)
                await producer.Queue.Push("rr-a", "Hello, World!", false);

            await Task.Delay(1200);
            Assert.Equal(1, c1);
            Assert.Equal(1, c2);
            Assert.Equal(1, c3);
            server.Stop();
        }
    }
}