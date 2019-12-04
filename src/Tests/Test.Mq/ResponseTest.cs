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
        public void FromClientToServer()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server waits a response but client does not send
        /// </summary>
        [Fact]
        public void FromClientToServerTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Server sends a response message to client
        /// </summary>
        [Fact]
        public void FromServerToClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client waits a response but server does not send
        /// </summary>
        [Fact]
        public void FromServerToClientTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a response message to other client
        /// </summary>
        [Fact]
        public void FromClientToClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client waits a response other client does not send
        /// </summary>
        [Fact]
        public void FromClientToClientTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a response message to channel
        /// </summary>
        [Fact]
        public void FromClientToChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Channel waits a response client does not send
        /// </summary>
        [Fact]
        public void FromClientToChannelTimeout()
        {
            throw new NotImplementedException();
        }
    }
}