using System;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class ClientOptionsTest
    {
        /// <summary>
        /// If true, every message must have an id even user does not set
        /// </summary>
        [Fact]
        public void UseUniqueMessageId()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42701);

            throw new NotImplementedException();
        }

        /// <summary>
        /// When a message with acknowledge required is received to client
        /// If auto acknowledge enabled, client should send an ack message automatically.
        /// </summary>
        [Fact]
        public void AutoAcknowledge()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42702);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes message received event of TmqClient.
        /// Sends a message and waits for response.
        /// If catching response is enabled, response message should trigger message received event.
        /// </summary>
        [Fact]
        public void CatchResponseMessages()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42703);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes a queue and sends a message to same queue.
        /// If ignore is enabled, message should be ignored.
        /// </summary>
        [Fact]
        public void IgnoreMyQueueMessages()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42704);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message and waits for acknowledge but server does not send acknowledge message. 
        /// </summary>
        [Fact]
        public void AcknowledgeTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42705);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message and waits the response but server does not send response. 
        /// </summary>
        [Fact]
        public void ResponseTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42706);

            throw new NotImplementedException();
        }
    }
}