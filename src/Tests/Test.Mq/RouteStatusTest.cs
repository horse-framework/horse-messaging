using System;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Xunit;

namespace Test.Mq
{
    public class RouteStatusTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public async Task SendToOnlineConsumers(int onlineConsumerCount)
        {
            int port = 47100 + onlineConsumerCount;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            int msgReceived = 0;

            for (int i = 0; i < onlineConsumerCount; i++)
            {
                TmqClient consumer = new TmqClient();
                consumer.ClientId = "consumer-" + i;
                await consumer.ConnectAsync("tmq://localhost:" + port);
                Assert.True(consumer.IsConnected);
                bool joined = await consumer.Join("ch-route", true);
                Assert.True(joined);
                consumer.MessageReceived += (c, m) => Interlocked.Increment(ref msgReceived);
            }

            await producer.Push("ch-route", MessageA.ContentType, "Hello, World!", false);
            await Task.Delay(1500);
            Assert.Equal(onlineConsumerCount, msgReceived);
        }

        [Fact]
        public async Task SendToOfflineConsumers()
        {
            int port = 47117;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            await producer.Push("ch-route", MessageA.ContentType, "Hello, World!", false);
            await Task.Delay(700);
            
            bool msgReceived = false;
            TmqClient consumer = new TmqClient();
            consumer.ClientId = "consumer";
            await consumer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(consumer.IsConnected);
            bool joined = await consumer.Join("ch-route", true);
            Assert.True(joined);
            consumer.MessageReceived += (c, m) => msgReceived = true;
            
            await Task.Delay(800);
            Assert.False(msgReceived);
        }

        [Fact]
        public async Task RequestAcknowledge()
        {
            throw new NotImplementedException();
        }
    }
}