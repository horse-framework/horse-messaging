using System;
using Xunit;

namespace Test.Mq
{
    public class ServerMessageTest
    {
        /// <summary>
        /// Client connects to server and each sends hello message
        /// </summary>
        [Fact]
        public void HelloBetweenServerClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a header message to server
        /// </summary>
        [Fact]
        public void HeaderFromClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server sends a header message to client
        /// </summary>
        [Fact]
        public void HeaderFromServer()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel join message to server
        /// </summary>
        [Fact]
        public void JoinChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel join message to server and waits response
        /// </summary>
        [Fact]
        public void JoinChannelWithResponse()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel leave message to server
        /// </summary>
        [Fact]
        public void LeaveChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel leave message to server and waits response
        /// </summary>
        [Fact]
        public void LeaveChannelWithResponse()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public void CreateQueue()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message and waits response
        /// </summary>
        [Fact]
        public void CreateQueueWithResponse()
        {
            throw new NotImplementedException();
        }
    }
}