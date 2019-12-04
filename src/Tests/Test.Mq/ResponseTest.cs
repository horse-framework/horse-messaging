using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class ResponseTest
    {
        /// <summary>
        /// Client sends a response message to server
        /// </summary>
        [Fact]
        public async Task FromClientToServer()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server waits a response but client does not send
        /// </summary>
        [Fact]
        public async Task FromClientToServerTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server sends a response message to client
        /// </summary>
        [Fact]
        public async Task FromServerToClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client waits a response but server does not send
        /// </summary>
        [Fact]
        public async Task FromServerToClientTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a response message to other client
        /// </summary>
        [Fact]
        public async Task FromClientToClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client waits a response other client does not send
        /// </summary>
        [Fact]
        public async Task FromClientToClientTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a response message to channel
        /// </summary>
        [Fact]
        public async Task FromClientToChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Channel waits a response client does not send
        /// </summary>
        [Fact]
        public async Task FromClientToChannelTimeout()
        {
            throw new NotImplementedException();
        }
    }
}