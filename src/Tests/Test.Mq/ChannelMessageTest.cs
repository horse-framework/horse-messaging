using System;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public void NonQueueMessageToClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public void NonQueueMessageToLateClients()
        {
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public void QueueMessageToClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public void ueueMessageToLateClients()
        {
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled and there are multiple receivers available
        /// </summary>
        [Fact]
        public void SendOnlyFirstMultipleClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available.
        /// They will join after message is sent.
        /// </summary>
        [Fact]
        public void SendOnlyFirstLateClients()
        {
            throw new NotImplementedException();
        }

        #endregion

        /// <summary>
        /// Sends message when UseMessageId option is enabled.
        /// </summary>
        [Fact]
        public void UseMessageId()
        {
            throw new NotImplementedException();
        }

        #region Wait For Acknowledge

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is no available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeNoClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeOneClient()
        {
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// </summary>
        [Fact]
        public void WaitAcknowledgeMultipleClients()
        {
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
            throw new NotImplementedException();
        }

        #endregion
    }
}