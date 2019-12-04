using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class ClientOptionsTest
    {
        /// <summary>
        /// If true, every message must have an id even user does not set
        /// </summary>
        [Fact]
        public async Task UseUniqueMessageId()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// When a message with acknowledge required is received to client
        /// If auto acknowledge enabled, client should send an ack message automatically.
        /// </summary>
        [Fact]
        public async Task AutoAcknowledge()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes message received event of TmqClient.
        /// Sends a message and waits for response.
        /// If catching response is enabled, response message should trigger message received event.
        /// </summary>
        [Fact]
        public async Task CatchResponseMessages()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes a queue and sends a message to same queue.
        /// If ignore is enabled, message should be ignored.
        /// </summary>
        [Fact]
        public async Task IgnoreMyQueueMessages()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message and waits for acknowledge but server does not send acknowledge message. 
        /// </summary>
        [Fact]
        public async Task AcknowledgeTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message and waits the response but server does not send response. 
        /// </summary>
        [Fact]
        public async Task ResponseTimeout()
        {
            throw new NotImplementedException();
        }
    }
}