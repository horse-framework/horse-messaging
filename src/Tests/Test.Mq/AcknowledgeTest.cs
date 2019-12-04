using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class AcknowledgeTest
    {
        #region Server - Client

        /// <summary>
        /// Sends message from client to server and wait for acknowledge from server to client by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromServerToClientAuto()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to server and wait for acknowledge from server to client by manuel
        /// </summary>
        [Fact]
        public async Task FromServerToClientManuel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to server and wait for acknowledge from server to client until timed out
        /// </summary>
        [Fact]
        public async Task FromServerToClientTimeout()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Client - Server

        /// <summary>
        /// Sends message from server to client and wait for acknowledge from client to server by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromClientToServerAuto()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from server to client and wait for acknowledge from client to server by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToServerManuel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from server to client and wait for acknowledge from client to server until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToServerTimeout()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Client - Client

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromClientToClientAuto()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToClientManuel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToClientTimeout()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Client - Channel

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromClientToChannelAuto()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToChannelManuel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToChannelTimeout()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}