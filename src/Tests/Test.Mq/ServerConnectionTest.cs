using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Mq
{
    public class ServerConnectionTest
    {
        /// <summary>
        /// Connects to TMQ Server and sends info message
        /// </summary>
        [Fact]
        public async Task ConnectWithInfo()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and does not send info message
        /// </summary>
        [Fact]
        public async Task ConnectWithoutInfo()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and sends another protocol message
        /// </summary>
        [Fact]
        public async Task ConnectAsOtherProtocol()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and does not send any message
        /// </summary>
        [Fact]
        public async Task ConnectWithoutSendingData()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive with PING and PONG messages
        /// </summary>
        [Fact]
        public async Task KeepAliveWithPingPong()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive until PING time out (does not send PONG message)
        /// </summary>
        [Fact]
        public async Task DisconnectDueToPingTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive a short duration and disconnects again with concurrent clients
        /// </summary>
        [Theory]
        [InlineData(100, 1000, 5000)]
        public async Task ConnectDisconnectStress(int concurrentClients, int minAliveMs, int maxAliveMs)
        {
            throw new NotImplementedException();
        }
    }
}