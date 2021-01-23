using System.Threading.Tasks;
using Test.Common;
using Horse.Mq.Client;
using Horse.Mq.Delivery;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;
using Xunit;

namespace Test.Queues
{
    public class PutBackTest
    {
        [Fact]
        public async Task Delayed()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("push-a");
            queue.Options.PutBackDelay = 2000;
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Start;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "First", false);
            await Task.Delay(100);
            await producer.Queues.Push("push-a", "Second", false);
            await Task.Delay(200);
            Assert.Equal(2, queue.MessageCount());

            int receivedMessages = 0;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += async (c, m) =>
            {
                receivedMessages++;
                await consumer.Queues.Unsubscribe("push-a", true);
                await Task.Delay(1000);
                await consumer.SendNegativeAck(m);
            };

            HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(1500);
            Assert.Equal(1, receivedMessages);
            Assert.Equal(1, queue.MessageCount());
            await Task.Delay(3000);
            Assert.Equal(2, queue.MessageCount());
        }
        
        [Fact]
        public async Task NoDelay()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("push-a");
            queue.Options.PutBackDelay = 0;
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Start;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "First", false);
            await Task.Delay(100);
            await producer.Queues.Push("push-a", "Second", false);
            await Task.Delay(200);
            Assert.Equal(2, queue.MessageCount());

            int receivedMessages = 0;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("hmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += async (c, m) =>
            {
                receivedMessages++;
                await consumer.Queues.Unsubscribe("push-a", true);
                await Task.Delay(1000);
                await consumer.SendNegativeAck(m);
            };

            HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(1500);
            Assert.Equal(1, receivedMessages);
            Assert.Equal(2, queue.MessageCount());
        }
    }
}