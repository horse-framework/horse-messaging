using System;
using Test.Mq.Internal;
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
        public void CreateMultipleQueuesInChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(43002);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates content type queues in a channel. (Allowed and not allowed)
        /// </summary>
        [Fact]
        public void CreateContentType()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(43003);

            throw new NotImplementedException();
        }
    }
}