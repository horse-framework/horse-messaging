using System;
using System.Threading;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Xunit;

namespace Test.Mq
{
    public class ServerConnectionTest
    {
        /// <summary>
        /// Connects to TMQ Server and sends info message
        /// </summary>
        [Fact]
        public void ConnectWithInfo()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42101);
            server.Start();

            TmqClient client = new TmqClient();
            client.Data.Properties.Add("Name", "Test-42101");
            client.Connect("tmq://localhost:42101/path");

            Thread.Sleep(50);

            Assert.True(client.IsConnected);
            Assert.Equal(1, server.ClientConnected);
        }

        /// <summary>
        /// Connects to TMQ Server and does not send info message
        /// </summary>
        [Fact]
        public void ConnectWithoutInfo()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and sends another protocol message
        /// </summary>
        [Fact]
        public void ConnectAsOtherProtocol()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and does not send any message
        /// </summary>
        [Fact]
        public void ConnectWithoutSendingData()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive with PING and PONG messages
        /// </summary>
        [Fact]
        public void KeepAliveWithPingPong()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive until PING time out (does not send PONG message)
        /// </summary>
        [Fact]
        public void DisconnectDueToPingTimeout()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Connects to TMQ Server and stays alive a short duration and disconnects again with concurrent clients
        /// </summary>
        [Theory]
        [InlineData(100, 1000, 5000)]
        public void ConnectDisconnectStress(int concurrentClients, int minAliveMs, int maxAliveMs)
        {
            throw new NotImplementedException();
        }
    }
}