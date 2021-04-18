using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Test.Common;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
using Xunit;

namespace Test.Queues
{
    public class DeliveryDelayTest
    {
        [Fact]
        public async Task Delay()
        {
            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("push-a");
            queue.Options.Acknowledge = QueueAckDecision.None;
            queue.Options.DelayBetweenMessages = 100;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            int receivedMessages = 0;
            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);
            consumer.MessageReceived += (c, m) => receivedMessages++;

            HorseResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            for (int i = 0; i < 30; i++)
                await producer.Queues.Push("push-a", "Hello, World!", false);

            await Task.Delay(500);
            Assert.True(receivedMessages > 4);
            Assert.True(receivedMessages < 7);
        }
    }
}