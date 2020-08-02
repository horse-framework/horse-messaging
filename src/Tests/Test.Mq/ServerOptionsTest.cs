using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.MQ;
using Twino.MQ.Queues;
using Xunit;

namespace Test.Mq
{
    public class ServerOptionsTest
    {
        /// <summary>
        /// Creates multiple queues in channel when it's supported/unsupported
        /// </summary>
        [Fact]
        public async Task CreateMultipleQueuesInChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            channel.Options.AllowMultipleQueues = true;
            channel.Options.AllowedQueues = null;

            ChannelQueue queue1 = await channel.CreateQueue(301);
            Assert.NotNull(queue1);

            channel.Options.AllowMultipleQueues = false;

            ChannelQueue queue2 = await channel.CreateQueue(302);
            Assert.Null(queue2);
        }

        /// <summary>
        /// Creates content type queues in a channel. (Allowed and not allowed)
        /// </summary>
        [Fact]
        public async Task CreateContentType()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            channel.Options.AllowedQueues = new[] { MessageA.ContentType, MessageB.ContentType, MessageC.ContentType };

            ChannelQueue queue1 = await channel.CreateQueue(MessageB.ContentType);
            Assert.NotNull(queue1);

            ChannelQueue queue2 = await channel.CreateQueue(305);
            Assert.Null(queue2);
        }
    }
}