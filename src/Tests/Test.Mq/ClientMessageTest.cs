using System;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class ClientMessageTest
    {
        /// <summary>
        /// Sends a client message and does not wait any ack or response
        /// </summary>
        [Fact]
        public void WithoutAnyResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42601);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a client message and waits acknowledge
        /// </summary>
        [Fact]
        public void WithAcknowledge()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42602);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a client message and waits response
        /// </summary>
        [Fact]
        public void WithResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42603);

            throw new NotImplementedException();
        }
    }
}