using System.Threading.Tasks;
using Test.Common;
using Twino.MQ.Client;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Queues
{
    public class DeliveryDelayTest
    {
        [Fact]
        public async Task Delay()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TwinoQueue queue = server.Server.FindQueue("push-a");
            queue.Options.Acknowledge = QueueAckDecision.None;
            queue.Options.DelayBetweenMessages = 100;

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int receivedMessages = 0;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => receivedMessages++;

            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            for (int i = 0; i < 30; i++)
                await producer.Queues.Push("push-a", "Hello, World!", false);

            await Task.Delay(500);
            Assert.True(receivedMessages > 4);
            Assert.True(receivedMessages < 7);
        }
    }
}