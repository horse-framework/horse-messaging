using System;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class MessageReaderTest
    {
        /// <summary>
        /// Clients subscribes to a queue and reads message with message reader
        /// </summary>
        [Fact]
        public void ClientReadsMessageFromQueue()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42801);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple channels
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleChannels()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42802);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple queues in same channel
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleQueues()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42803);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Reader attaches multiple clients
        /// </summary>
        [Fact]
        public void MultipleAttachOnSameReader()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42804);

            throw new NotImplementedException();
        }

        /// <summary>
        /// End user throws an unhandled exception in attached method
        /// </summary>
        [Fact]
        public void ExceptionOnBindMethod()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42805);

            throw new NotImplementedException();
        }
    }
}