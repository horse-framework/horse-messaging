using System;
using System.Threading.Tasks;
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
        public async Task NonQueueMessageToNoClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task NonQueueMessageToClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public async Task NonQueueMessageToLateClients()
        {
            throw new NotImplementedException();
        }

        #endregion


        #region Queuing

        /// <summary>
        /// Message is sent when there aren't any clients
        /// </summary>
        [Fact]
        public async Task QueueMessageToNoClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task QueueMessageToClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public async Task ueueMessageToLateClients()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Send Only First

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available
        /// </summary>
        [Fact]
        public async Task SendOnlyFirstNoClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled and there are multiple receivers available
        /// </summary>
        [Fact]
        public async Task SendOnlyFirstMultipleClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available.
        /// They will join after message is sent.
        /// </summary>
        [Fact]
        public async Task SendOnlyFirstLateClients()
        {
            throw new NotImplementedException();
        }

        #endregion

        /// <summary>
        /// Sends message when UseMessageId option is enabled.
        /// </summary>
        [Fact]
        public async Task UseMessageId()
        {
            throw new NotImplementedException();
        }

        #region Wait For Acknowledge

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is no available receiver.
        /// </summary>
        [Fact]
        public async Task WaitAcknowledgeNoClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// </summary>
        [Fact]
        public async Task WaitAcknowledgeOneClient()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// But it does not send acknowledge message
        /// </summary>
        [Fact]
        public async Task WaitAcknowledgeOneClientWithNoAck()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// </summary>
        [Fact]
        public async Task WaitAcknowledgeMultipleClients()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// But they do not send acknowledge message
        /// </summary>
        [Fact]
        public async Task WaitAcknowledgeMultipleClientsWithNoAck()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}