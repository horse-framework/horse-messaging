using System;
using System.IO;
using System.Linq;
using System.Text;
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
    public class MessagingOptionsTest
    {
        #region Route Messaging

        /// <summary>
        /// Message is sent when there aren't any clients
        /// </summary>
        [Fact]
        public async Task RouteToNoClients()
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
            bool sent = await client.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.RegularMessages);
            Assert.False(received);
        }

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task RouteToClients()
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
            bool sent = await client.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(received);
        }

        #endregion

        #region Push Messaging

        /// <summary>
        /// Message is sent one or multiple available clients
        /// </summary>
        [Fact]
        public async Task PushToClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42505);
            server.Server.Options.Status = QueueStatus.Push;
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
            bool sent = await client.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(received);
        }

        /// <summary>
        /// Clients will join after messages are sent
        /// </summary>
        [Fact]
        public async Task PushToLateClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42506);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42506");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            await queue.SetStatus(QueueStatus.Push);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);
            Assert.NotEmpty(queue.RegularMessages);
            Assert.False(received);

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.RegularMessages);
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

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.SendOnlyFirstAcquirer = true;

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
            bool sent = await client1.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);

            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
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

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.SendOnlyFirstAcquirer = true;
            await queue.SetStatus(QueueStatus.Push);

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
            bool sent = await client1.Push(channel.Name, queue.Id, ms, false);
            Assert.True(sent);

            await Task.Delay(1500);

            bool joined = await client1.Join(channel.Name, true);
            Assert.True(joined);
            joined = await client2.Join(channel.Name, true);
            Assert.True(joined);
            joined = await client3.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(250);

            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            int c = received.Count(x => x);
            Assert.Equal(1, c);
        }

        #endregion

        #region Non Queue Acknowledge

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is no available receiver.
        /// </summary>
        [Fact]
        public async Task NonQueueWaitAcknowledgeNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42511);
            server.Start();
            server.Server.Options.RequestAcknowledge = true;
            server.Server.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42511");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.Id, ms, true);
            Assert.False(sent);

            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.RegularMessages);
            Assert.False(received);
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// </summary>
        [Fact]
        public async Task NonQueueWaitAcknowledgeOneClient()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42512);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42512");
            client.AutoAcknowledge = true;
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(6);

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
            bool sent = await client.Push(channel.Name, queue.Id, ms, true);

            Assert.True(sent);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(received);
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// </summary>
        [Fact]
        public async Task NonQueueWaitAcknowledgeMultipleClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42514);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();
            client1.AutoAcknowledge = true;
            client2.AutoAcknowledge = true;
            await client1.ConnectAsync("tmq://localhost:42514");
            await client2.ConnectAsync("tmq://localhost:42514");
            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(6);

            bool joined1 = await client1.Join(channel.Name, true);
            bool joined2 = await client2.Join(channel.Name, true);
            Assert.True(joined1);
            Assert.True(joined2);
            await Task.Delay(250);

            bool receive1 = false;
            bool receive2 = false;
            client1.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    receive1 = true;
            };
            client2.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    receive2 = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client1.Push(channel.Name, queue.Id, ms, true);

            await Task.Delay(250);

            Assert.True(sent);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(receive1);
            Assert.True(receive2);
        }

        #endregion

        #region Queue Acknowledge

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is no available receiver.
        /// </summary>
        [Fact]
        public async Task QueueWaitAcknowledgeNoClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42521);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42521");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            await queue.SetStatus(QueueStatus.Push);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.Id, ms, true);

            Assert.False(sent);
            Assert.NotEmpty(queue.RegularMessages);

            bool received = false;
            client.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    received = true;
            };

            bool joined = await client.Join(channel.Name, true);
            Assert.True(joined);
            await Task.Delay(1500);

            Assert.Empty(queue.RegularMessages);
            Assert.True(received);
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There is one available receiver.
        /// </summary>
        [Fact]
        public async Task QueueWaitAcknowledgeOneClient()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(40582);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:40582");
            client.AutoAcknowledge = true;
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.Status = QueueStatus.Push;
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(6);

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
            bool sent = await client.Push(channel.Name, queue.Id, ms, true);

            Assert.True(sent);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(received);
        }

        /// <summary>
        /// Sends message and wait for acknowledge to go on other queue messages.
        /// There are multiple available receiver.
        /// </summary>
        [Fact]
        public async Task QueueWaitAcknowledgeMultipleClients()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42594);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();
            client1.AutoAcknowledge = true;
            client2.AutoAcknowledge = true;
            await client1.ConnectAsync("tmq://localhost:42594");
            await client2.ConnectAsync("tmq://localhost:42594");
            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            
            queue.Options.Status = QueueStatus.Push;
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(6);

            bool joined1 = await client1.Join(channel.Name, true);
            bool joined2 = await client2.Join(channel.Name, true);
            Assert.True(joined1);
            Assert.True(joined2);
            await Task.Delay(250);

            bool receive1 = false;
            bool receive2 = false;
            client1.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    receive1 = true;
            };
            client2.MessageReceived += (c, m) =>
            {
                if (m.Type == MessageType.Channel)
                    receive2 = true;
            };

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client1.Push(channel.Name, queue.Id, ms, true);

            await Task.Delay(250);

            Assert.True(sent);
            Assert.Empty(queue.HighPriorityMessages);
            Assert.Empty(queue.RegularMessages);
            Assert.True(receive1);
            Assert.True(receive2);
        }

        #endregion

        #region Hide Names

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HideNamesInChannel(bool enabled)
        {
            int port = enabled ? 42531 : 42532;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300);

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);
            
            queue.Options.HideClientNames = enabled;
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            client.AutoAcknowledge = true;
            client.CatchAcknowledgeMessages = true;
            Assert.True(client.IsConnected);

            bool joined = await client.Join("ch-1", true);
            Assert.True(joined);

            TmqMessage received = null;
            TmqMessage ack = null;
            client.MessageReceived += (c, m) =>
            {
                switch (m.Type)
                {
                    case MessageType.Channel:
                        received = m;
                        break;
                    case MessageType.Acknowledge:
                        ack = m;
                        break;
                }
            };

            await Task.Delay(500);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push("ch-1", MessageA.ContentType, ms, true);
            Assert.True(sent);

            await Task.Delay(1000);

            Assert.NotNull(received);
            Assert.NotNull(ack);

            Assert.Equal("ch-1", received.Target);
            Assert.Equal("ch-1", ack.Target);

            if (enabled)
            {
                Assert.Null(received.Source);
                Assert.Null(ack.Source);
            }
            else
            {
                Assert.Equal(client.ClientId, received.Source);
                Assert.Equal(client.ClientId, ack.Source);
            }
        }

        #endregion

        [Fact]
        public async Task SendAcknowledgeFromServerToProducer()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42599);
            server.Start();
            server.SendAcknowledgeFromMQ = true;
            
            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:42599");
            Assert.True(client.IsConnected);
            
            bool ack = await client.Push("ch-route", MessageA.ContentType, "Hello", true);
            Assert.True(ack);
        }
    }
}