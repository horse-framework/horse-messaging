using System;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple channels
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleChannels()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple queues in same channel
        /// </summary>
        [Fact]
        public void ClientReadsMessagesFromMultipleQueues()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reader attaches multiple clients
        /// </summary>
        [Fact]
        public void MultipleAttachOnSameReader()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// End user throws an unhandled exception in attached method
        /// </summary>
        [Fact]
        public void ExceptionOnBindMethod()
        {
            throw new NotImplementedException();
        }
    }
}