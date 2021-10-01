using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Queues
{
    public class QueueAckTest
    {
        [Fact]
        public async Task AckTimeout()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queue.Push("push-a", "Hello, World!", false);
            await Task.Delay(100);

            HorseClient consumer = new HorseClient();
            consumer.ClientId = "consumer";
            consumer.AutoAcknowledge = false;

            int msgReceived = 0;
            consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);

            await consumer.ConnectAsync("horse://localhost:" + port);
            Assert.True(consumer.IsConnected);

            HorseResult joined = await consumer.Queue.Subscribe("push-a", true);
            Assert.Equal(HorseResultCode.Ok, joined.Code);

            await Task.Delay(100);
            Assert.Equal(1, msgReceived);
            consumer.Disconnect();

            await Task.Delay(1500);
            Assert.Equal(1, server.OnAcknowledgeTimeUp);
            server.Stop();
        }
    }
}