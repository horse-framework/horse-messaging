using System;
using Test.Mq.Internal;
using Xunit;

namespace Test.Mq
{
    public class ChannelMessageTest
    {
        #region Not Queuing

        /// <summary>
        /// Message is sent when there aren't any clients
        /// </summary>
        [Fact]
        public void NonQueueMessageToNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42501);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public void NonQueueMessageToClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42502);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public void NonQueueMessageToLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42503);

            throw new NotImplementedException();
        }

        #endregion

        #region Queuing

        /// <summary>
        /// Message is sent when there aren't any clients
        /// </summary>
        [Fact]
        public void QueueMessageToNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42504);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public void QueueMessageToClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42505);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public void ueueMessageToLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42506);

            throw new NotImplementedException();
        }

        #endregion

        #region Send Only First

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available
        /// </summary>
        [Fact]
        public void SendOnlyFirstNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42507);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled and there are multiple receivers available
        /// </summary>
        [Fact]
        public void SendOnlyFirstMultipleClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42508);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available.
        /// They will join after message is sent.
        /// </summary>
        [Fact]
        public void SendOnlyFirstLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42509);

            throw new NotImplementedException();
        }

        #endregion

        #region Wait For Acknowledge

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is no available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42511);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeOneClient()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42512);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// But it does not send acknowledge message
        /// </summary>
        [Fact]
        public void WaitAcknowledgeOneClientWithNoAck()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42513);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeMultipleClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42514);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// But they do not send acknowledge message
        /// </summary>
        [Fact]
        public void WaitAcknowledgeMultipleClientsWithNoAck()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42515);

            throw new NotImplementedException();
        }

        #endregion
    }
}