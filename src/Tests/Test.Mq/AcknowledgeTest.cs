using System;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class AcknowledgeTest
    {
        #region Client - Client

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by AutoAcknowledge property
        /// </summary>
        [Fact]
        public void FromClientToClientAuto()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42301);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by manuel
        /// </summary>
        [Fact]
        public void FromClientToClientManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42302);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client until timed out
        /// </summary>
        [Fact]
        public void FromClientToClientTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42303);

            throw new NotImplementedException();
        }

        #endregion

        #region Client - Channel

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by AutoAcknowledge property
        /// </summary>
        [Fact]
        public void FromClientToChannelAuto()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42304);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by manuel
        /// </summary>
        [Fact]
        public void FromClientToChannelManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42305);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel until timed out
        /// </summary>
        [Fact]
        public void FromClientToChannelTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42306);

            throw new NotImplementedException();
        }

        #endregion
    }
}