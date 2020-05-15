using System;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    public class QueueManagementTest
    {
        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Create(bool verifyResponse)
        {
            int port = verifyResponse ? 40905 : 40904;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            TwinoResult created = await client.CreateQueue("ch-2", MessageA.ContentType, verifyResponse);
            Assert.Equal(TwinoResult.Ok, created);
            await Task.Delay(1000);

            Channel channel = server.Server.Channels.FirstOrDefault(x => x.Name == "ch-2");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            Assert.Equal(MessageA.ContentType, queue.Id);
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

            TwinoResult created = await client.CreateQueue("ch-test", MessageA.ContentType, true, o =>
            {
                o.SendOnlyFirstAcquirer = true;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(33);
                o.Status = MessagingQueueStatus.Pull;
            });
            Assert.Equal(TwinoResult.Ok, created);

            Channel channel = server.Server.FindChannel("ch-test");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            Assert.True(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(33), queue.Options.AcknowledgeTimeout);
            Assert.Equal(QueueStatus.Pull, queue.Status);
        }

        [Fact]
        public async Task Update()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41207);
            server.Start();

            Channel channel = server.Server.FindChannel("ch-route");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            Assert.False(queue.Options.WaitForAcknowledge);
            Assert.False(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(12), queue.Options.MessageTimeout);

            TmqAdminClient client = new TmqAdminClient();
            await client.ConnectAsync("tmq://localhost:41207");
            Assert.True(client.IsConnected);

            TwinoResult updated = await client.SetQueueOptions("ch-route", MessageA.ContentType, o =>
            {
                o.WaitForAcknowledge = true;
                o.MessageTimeout = TimeSpan.FromSeconds(666);
                o.SendOnlyFirstAcquirer = true;
            });
            Assert.Equal(TwinoResult.Ok, updated);

            Assert.True(queue.Options.WaitForAcknowledge);
            Assert.True(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(666), queue.Options.MessageTimeout);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Remove(bool verifyResponse)
        {
            int port = 41208 + Convert.ToInt32(verifyResponse);
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            Channel channel = server.Server.FindChannel("ch-route");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            TmqAdminClient client = new TmqAdminClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);

            TwinoResult done = await client.RemoveQueue("ch-route", MessageA.ContentType, verifyResponse);
            Assert.Equal(TwinoResult.Ok, done);

            if (!verifyResponse)
                await Task.Delay(500);

            queue = channel.FindQueue(MessageA.ContentType);
            Assert.Null(queue);
        }
    }
}