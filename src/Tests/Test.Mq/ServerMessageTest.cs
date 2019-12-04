using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class ServerMessageTest
    {
        /// <summary>
        /// Client connects to server and each sends hello message
        /// </summary>
        [Fact]
        public async Task HelloBetweenServerClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a header message to server
        /// </summary>
        [Fact]
        public async Task HeaderFromClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server sends a header message to client
        /// </summary>
        [Fact]
        public async Task HeaderFromServer()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel join message to server
        /// </summary>
        [Fact]
        public async Task JoinChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel join message to server and waits response
        /// </summary>
        [Fact]
        public async Task JoinChannelWithResponse()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel leave message to server
        /// </summary>
        [Fact]
        public async Task LeaveChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel leave message to server and waits response
        /// </summary>
        [Fact]
        public async Task LeaveChannelWithResponse()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public async Task CreateQueue()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message and waits response
        /// </summary>
        [Fact]
        public async Task CreateQueueWithResponse()
        {
            throw new NotImplementedException();
        }
    }
}