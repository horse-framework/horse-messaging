using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.Protocols.TMQ;
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
            TestMqServer server = new TestMqServer();
            server.Initialize(42501);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42501");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.PrefentialMessages);
            Assert.Empty(queue.StandardMessages);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.StandardMessages);
            Assert.False(received);
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task NonQueueMessageToClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42502);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42502");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(250);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.PrefentialMessages);
            Assert.Empty(queue.StandardMessages);
            Assert.True(received);
        }

        #endregion

        #region Queuing

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task QueueMessageToClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42505);
            server.Server.Options.MessageQueuing = true;
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42505");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(250);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.PrefentialMessages);
            Assert.Empty(queue.StandardMessages);
            Assert.True(received);
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public async Task QueueMessageToLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42506);
            server.Server.Options.MessageQueuing = true;
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42506");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.NotEmpty(queue.StandardMessages);
            Assert.False(received);

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.StandardMessages);
            Assert.True(received);
        }

        #endregion

        #region Send Only First

        /// <summary>
        /// Sends message when SendOnlyFirst enabled and there are multiple receivers available
        /// </summary>
        [Fact]
        public async Task SendOnlyFirstMultipleClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42508);
            server.Start();
            server.Server.Options.SendOnlyFirstAcquirer = true;

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            bool[] received = new bool[3];

            async Task<TmqClient> join(int no)
            {
                TmqClient client = new TmqClient();
                client.ClientId = "client-" + no;
                await client.ConnectAsync("tmq://localhost:42508");
                Assert.True(client.IsConnected);

                client.MessageReceived += (cx, m) =>
                {
                    if (m.Type == MessageType.Channel)
                        received[no - 1] = true;
                };

                bool joined = await client.Join(channel.Name, true);
                Assert.True(joined);
                await Task.Delay(250);
                return client;
            }

            TmqClient client1 = await join(1);
            await join(2);
            await join(3);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client1.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);

            Assert.Empty(queue.PrefentialMessages);
            Assert.Empty(queue.StandardMessages);
            int c = received.Count(x => x);
            Assert.Equal(1, c);
        }

        /// <summary>
        /// Sends message when SendOnlyFirst enabled but there are no receivers available.
        /// They will join after message is sent.
        /// </summary>
        [Fact]
        public async Task SendOnlyFirstLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42509);
            server.Start();
            server.Server.Options.MessageQueuing = true;
            server.Server.Options.SendOnlyFirstAcquirer = true;

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            bool[] received = new bool[3];

            async Task<TmqClient> join(int no)
            {
                TmqClient client = new TmqClient();
                client.ClientId = "client-" + no;
                await client.ConnectAsync("tmq://localhost:42509");
                Assert.True(client.IsConnected);

                client.MessageReceived += (cx, m) =>
                {
                    if (m.Type == MessageType.Channel)
                        received[no - 1] = true;
                };

                return client;
            }

            TmqClient client1 = await join(1);
            TmqClient client2 = await join(2);
            TmqClient client3 = await join(3);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client1.Push(channel.Name, queue.ContentType, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);

            bool joined = await client1.Join(channel.Name, true);
            Assert.True(joined);
            joined = await client2.Join(channel.Name, true);
            Assert.True(joined);
            joined = await client3.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(250);

            Assert.Empty(queue.PrefentialMessages);
            Assert.Empty(queue.StandardMessages);
            int c = received.Count(x => x);
            Assert.Equal(1, c);
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