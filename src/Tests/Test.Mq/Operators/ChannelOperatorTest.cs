using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Models;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Operators
{
    public class ChannelOperatorTest
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
            await client.ConnectAsync("tmq://localhost:41201");

            TwinoResult joined = await client.Channels.Join("ch-1", false);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);
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
            server.Initialize(41202);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:41202");

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            List<ChannelClient> clients = channel.ClientsClone;
            Assert.Single(clients);
        }

        /// <summary>
        /// Client sends a channel leave message to server
        /// </summary>
        [Fact]
        public async Task LeaveChannel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41203);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:41203");

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TwinoResult left = await client.Channels.Leave("ch-1", false);
            Assert.Equal(TwinoResultCode.Ok, left.Code);
            await Task.Delay(1000);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            List<ChannelClient> clients = channel.ClientsClone;
            Assert.Empty(clients);
        }

        /// <summary>
        /// Client sends a channel leave message to server and waits response
        /// </summary>
        [Fact]
        public async Task LeaveChannelWithResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41204);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:41204");

            TwinoResult joined = await client.Channels.Join("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, joined.Code);

            TwinoResult left = await client.Channels.Leave("ch-1", true);
            Assert.Equal(TwinoResultCode.Ok, left.Code);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            List<ChannelClient> clients = channel.ClientsClone;
            Assert.Empty(clients);
        }

        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public async Task Create()
        {
            int port = 35905;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            TwinoResult created = await client.Channels.Create("new-channel");
            Assert.Equal(TwinoResultCode.Ok, created.Code);
        }

        [Fact]
        public async Task Delete()
        {
            int port = 35965;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            server.Server.CreateChannel("new-channel");

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            TwinoResult deleted = await client.Channels.Remove("new-channel");
            Assert.Equal(TwinoResultCode.Ok, deleted.Code);

            Assert.Null(server.Server.FindChannel("new-channel"));
        }

        [Fact]
        public async Task CreateWithProperties()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41206);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:41206");
            Assert.True(client.IsConnected);

            TwinoResult created = await client.Channels.Create("new-channel", o =>
            {
                o.AllowMultipleQueues = false;
                o.SendOnlyFirstAcquirer = true;
                o.AcknowledgeTimeout = 33000;
                o.Status = MessagingQueueStatus.Pull;
            });
            Assert.Equal(TwinoResultCode.Ok, created.Code);

            Channel found = server.Server.FindChannel("new-channel");
            Assert.NotNull(found);
            Assert.False(found.Options.AllowMultipleQueues);
            Assert.True(found.Options.SendOnlyFirstAcquirer);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("ch-pull")]
        [InlineData("*h-pu*")]
        public async Task FindChannels(string filter)
        {
            int port = 35941 + new Random().Next(20);
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            var channels = await client.Channels.List(filter);
            Assert.Equal(TwinoResultCode.Ok, channels.Result.Code);
            Assert.NotEmpty(channels.Model);
        }

        [Fact]
        public async Task GetChannelInfo()
        {
            int port = 35959;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            var channels = await client.Channels.GetInfo("ch-push");
            Assert.Equal(TwinoResultCode.Ok, channels.Result.Code);
            Assert.NotNull(channels.Model);
            Assert.Equal("ch-push", channels.Model.Name);
        }
    }
}