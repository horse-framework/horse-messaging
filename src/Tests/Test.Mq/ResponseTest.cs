using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class ResponseTest
    {
        /// <summary>
        /// Client sends a response message to other client
        /// </summary>
        [Fact]
        public void FromClientToClient()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42401);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Client waits a response other client does not send
        /// </summary>
        [Fact]
        public void FromClientToClientTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42402);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a response message to channel
        /// </summary>
        [Fact]
        public void FromClientToChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42403);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Channel waits a response client does not send
        /// </summary>
        [Fact]
        public void FromClientToChannelTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42404);

            throw new NotImplementedException();
        }
    }
}