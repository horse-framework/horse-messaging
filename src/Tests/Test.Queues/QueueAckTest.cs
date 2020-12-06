using System.Threading;
using System.Threading.Tasks;
using Test.Common;
using Twino.MQ.Client;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Queues
{
    public class QueueAckTest
    {
        [Fact]
        public async Task AckTimeout()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Queues.Push("push-a", "Hello, World!", false);
            await Task.Delay(100);

            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            consumer.AutoAcknowledge = false;

            int msgReceived = 0;
            consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);

            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);

            TwinoResult joined = await consumer.Queues.Subscribe("push-a", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            await Task.Delay(100);
            Assert.Equal(1, msgReceived);
            consumer.Disconnect();

            await Task.Delay(1500);
            Assert.Equal(1, server.OnAcknowledgeTimeUp);
        }
    }
}