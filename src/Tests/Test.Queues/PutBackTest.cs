using System.Threading.Tasks;
using Test.Common;
using Twino.MQ.Client;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Queues
{
    public class PutBackTest
    {
        [Fact]
        public async Task Delayed()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TwinoQueue queue = server.Server.FindQueue("push-a");
            queue.Options.PutBackDelay = 2000;
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Start;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "First", false);
            await Task.Delay(100);
            await producer.Queues.Push("push-a", "Second", false);
            await Task.Delay(200);
            Assert.Equal(2, queue.MessageCount());

            int receivedMessages = 0;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += async (c, m) =>
            {
                receivedMessages++;
                await consumer.Queues.Unsubscribe("push-a", true);
                await Task.Delay(1000);
                await consumer.SendNegativeAck(m);
            };

            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(1500);
            Assert.Equal(1, receivedMessages);
            Assert.Equal(1, queue.MessageCount());
            await Task.Delay(3000);
            Assert.Equal(2, queue.MessageCount());
        }
        
        [Fact]
        public async Task NoDelay()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TwinoQueue queue = server.Server.FindQueue("push-a");
            queue.Options.PutBackDelay = 0;
            queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            server.PutBack = PutBackDecision.Start;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "First", false);
            await Task.Delay(100);
            await producer.Queues.Push("push-a", "Second", false);
            await Task.Delay(200);
            Assert.Equal(2, queue.MessageCount());

            int receivedMessages = 0;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += async (c, m) =>
            {
                receivedMessages++;
                await consumer.Queues.Unsubscribe("push-a", true);
                await Task.Delay(1000);
                await consumer.SendNegativeAck(m);
            };

            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(1500);
            Assert.Equal(1, receivedMessages);
            Assert.Equal(2, queue.MessageCount());
        }
    }
}