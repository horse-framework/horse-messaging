using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    /// <summary>
    /// Ports 42300 - 42310
    /// </summary>
    public class AcknowledgeTest
    {
        #region Client - Client

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromClientToClientAuto()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42301);
            server.Start();

            server.Server.Server.Options.PingInterval = 300;
            server.Server.Server.Options.RequestTimeout = 300;
            await Task.Delay(250);

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = true;

            await client1.ConnectAsync("tmq://localhost:42301");
            await client2.ConnectAsync("tmq://localhost:42301");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            TmqMessage message = new TmqMessage();
            message.HighPriority = true;
            message.Type = MessageType.DirectMessage;
            message.SetTarget(client2.ClientId);
            message.SetStringContent("Hello, World!");

            TwinoResult acknowledge = await client1.SendWithAcknowledge(message);
            Assert.Equal(TwinoResultCode.Ok, acknowledge.Code);
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToClientManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42302);
            server.Start();

            server.Server.Server.Options.PingInterval = 300;
            server.Server.Server.Options.RequestTimeout = 300;
            await Task.Delay(250);

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = false;
            client2.MessageReceived += async (c, m) =>
            {
                if (m.PendingAcknowledge)
                    await client2.SendAsync(m.CreateAcknowledge());
            };

            await client1.ConnectAsync("tmq://localhost:42302");
            await client2.ConnectAsync("tmq://localhost:42302");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            TmqMessage message = new TmqMessage();
            message.HighPriority = true;
            message.Type = MessageType.DirectMessage;
            message.SetTarget(client2.ClientId);
            message.SetStringContent("Hello, World!");

            TwinoResult acknowledge = await client1.SendWithAcknowledge(message);
            Assert.Equal(TwinoResultCode.Ok, acknowledge.Code);
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToClientTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42307);
            server.Start(15, 15);

            server.Server.Server.Options.PingInterval = 300;
            server.Server.Server.Options.RequestTimeout = 300;
            await Task.Delay(250);

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client1.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            client2.AutoAcknowledge = false;

            await client1.ConnectAsync("tmq://localhost:42307");
            await client2.ConnectAsync("tmq://localhost:42307");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            TmqMessage message = new TmqMessage();
            message.HighPriority = true;
            message.Type = MessageType.DirectMessage;
            message.SetTarget(client2.ClientId);
            message.SetStringContent("Hello, World!");

            TwinoResult acknowledge = await client1.SendWithAcknowledge(message);
            Assert.NotEqual(TwinoResultCode.Ok, acknowledge.Code);
        }

        #endregion

        #region Client - Channel

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by AutoAcknowledge property
        /// </summary>
        [Fact]
        public async Task FromClientToChannelAuto()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42304);
            server.Start();

            TmqClient client = new TmqClient();
            client.AutoAcknowledge = true;

            await client.ConnectAsync("tmq://localhost:42304");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);
            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            queue.Options.RequestAcknowledge = true;

            //subscribe
            await client.Channels.Join(channel.Name, true);
            await Task.Delay(250);

            //push a message to the queue
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            TwinoResult sent = await client.Queues.Push(channel.Name, queue.Id, ms, true);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToChannelManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42305);
            server.Start();

            TmqClient client = new TmqClient();
            client.AutoAcknowledge = false;
            client.MessageReceived += async (c, m) => { await client.SendAsync(m.CreateAcknowledge()); };

            await client.ConnectAsync("tmq://localhost:42305");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);
            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            queue.Options.RequestAcknowledge = true;

            //subscribe
            await client.Channels.Join(channel.Name, true);

            //push a message to the queue
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            TwinoResult sent = await client.Queues.Push(channel.Name, queue.Id, ms, true);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToChannelTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42306);
            server.Start();

            TmqClient client = new TmqClient();
            client.AutoAcknowledge = false;
            client.AcknowledgeTimeout = TimeSpan.FromSeconds(3);

            await client.ConnectAsync("tmq://localhost:42306");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);
            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            queue.Options.RequestAcknowledge = true;

            //subscribe
            await client.Channels.Join(channel.Name, true);

            //push a message to the queue
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            TwinoResult sent = await client.Queues.Push(channel.Name, queue.Id, ms, true);
            Assert.NotEqual(TwinoResultCode.Ok, sent.Code);
        }

        #endregion
    }
}