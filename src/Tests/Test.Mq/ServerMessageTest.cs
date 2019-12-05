using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Clients;
using Xunit;

namespace Test.Mq
{
    public class ServerMessageTest
    {
        /// <summary>
        /// Client sends a channel join message to server
        /// </summary>
        [Fact]
        public async Task JoinChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41201);
            server.Start();

            TmqClient client = new TmqClient();
            client.Connect("tmq://localhost:41201");

            bool joined = await client.Join("ch-1", false);
            Assert.True(joined);
            await Task.Delay(1000);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            List<ChannelClient> clients = channel.ClientsClone;
            Assert.Single(clients);
        }

        /// <summary>
        /// Client sends a channel join message to server and waits response
        /// </summary>
        [Fact]
        public async Task JoinChannelWithResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41201);
            server.Start();

            TmqClient client = new TmqClient();
            client.Connect("tmq://localhost:41201");

            bool joined = await client.Join("ch-1", true);
            Assert.True(joined);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            List<ChannelClient> clients = channel.ClientsClone;
            Assert.Single(clients);
        }

        /// <summary>
        /// Client sends a channel leave message to server
        /// </summary>
        [Fact]
        public void LeaveChannel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a channel leave message to server and waits response
        /// </summary>
        [Fact]
        public void LeaveChannelWithResponse()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public void CreateQueue()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Client sends a queue creation message and waits response
        /// </summary>
        [Fact]
        public void CreateQueueWithResponse()
        {
            throw new NotImplementedException();
        }
    }
}