using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class MessageReaderTest
    {
        /// <summary>
        /// Clients subscribes to a queue and reads message with message reader
        /// </summary>
        [Fact]
        public async Task ClientReadsMessageFromQueue()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple channels
        /// </summary>
        [Fact]
        public async Task ClientReadsMessagesFromMultipleChannels()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client reads messages from multiple queues in same channel
        /// </summary>
        [Fact]
        public async Task ClientReadsMessagesFromMultipleQueues()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reader attaches multiple clients
        /// </summary>
        [Fact]
        public async Task MultipleAttachOnSameReader()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// End user throws an unhandled exception in attached method
        /// </summary>
        [Fact]
        public async Task ExceptionOnBindMethod()
        {
            throw new NotImplementedException();
        }
    }
}