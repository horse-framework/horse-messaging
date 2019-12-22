using System;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Xunit;

namespace Test.Mq
{
    public class QueueManagementTest
    {
        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Create(bool verifyResponse)
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41205);
            server.Start();

            TmqClient client = new TmqClient();
            client.Connect("tmq://localhost:41205");

            bool created = await client.CreateQueue("ch-2", MessageA.ContentType, verifyResponse);
            Assert.True(created);
            await Task.Delay(1000);

            Channel channel = server.Server.Channels.FirstOrDefault(x => x.Name == "ch-2");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            Assert.Equal(MessageA.ContentType, queue.ContentType);
        }

        [Fact]
        public async Task CreateWithProperties()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task Update()
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Remove(bool verifyResponse)
        {
            throw new NotImplementedException();
        }
    }
}