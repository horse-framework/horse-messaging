using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.MQ;
using Xunit;

namespace Test.Mq
{
    public class ServerOptionsTest
    {
        /// <summary>
        /// Sends a channel message when hide client names enabled
        /// </summary>
        [Fact]
        public void HideClientNames()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(43001);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates multiple queues in channel when it's supported/unsupported
        /// </summary>
        [Fact]
        public async Task CreateMultipleQueuesInChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(43002);

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            channel.Options.AllowMultipleQueues = true;
            channel.Options.AllowedContentTypes = null;
            
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
            server.Initialize(43003);

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            channel.Options.AllowedContentTypes = new[] {MessageA.ContentType, MessageB.ContentType, MessageC.ContentType};
            
            ChannelQueue queue1 = await channel.CreateQueue(MessageB.ContentType);
            Assert.NotNull(queue1);
            
            ChannelQueue queue2 = await channel.CreateQueue(305);
            Assert.Null(queue2);
        }
    }
}