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
            server.Server.ServerOptions.PingInterval = 300;
            server.Server.ServerOptions.RequestTimeout = 300;

            server.Start();
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
            message.Type = MessageType.Client;
            message.Target = client2.ClientId;
            message.SetStringContent("Hello, World!");

            bool acknowledge = await client1.SendWithAcknowledge(message);
            Assert.True(acknowledge);
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client by manuel
        /// </summary>
        [Fact]
        public async Task FromClientToClientManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42302);
            server.Server.ServerOptions.PingInterval = 300;
            server.Server.ServerOptions.RequestTimeout = 300;

            server.Start();
            await Task.Delay(250);

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = false;
            client2.MessageReceived += async (c, m) =>
            {
                if (m.AcknowledgeRequired)
                    await client2.SendAsync(m.CreateAcknowledge());
            };

            await client1.ConnectAsync("tmq://localhost:42302");
            await client2.ConnectAsync("tmq://localhost:42302");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            TmqMessage message = new TmqMessage();
            message.HighPriority = true;
            message.Type = MessageType.Client;
            message.Target = client2.ClientId;
            message.SetStringContent("Hello, World!");

            bool acknowledge = await client1.SendWithAcknowledge(message);
            Assert.True(acknowledge);
        }

        /// <summary>
        /// Sends message from client to other client and wait for acknowledge from other client to client until timed out
        /// </summary>
        [Fact]
        public async Task FromClientToClientTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42303);
            server.Server.ServerOptions.PingInterval = 300;
            server.Server.ServerOptions.RequestTimeout = 300;

            server.Start();
            await Task.Delay(250);

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client1.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            client2.AutoAcknowledge = false;

            await client1.ConnectAsync("tmq://localhost:42303");
            await client2.ConnectAsync("tmq://localhost:42303");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            TmqMessage message = new TmqMessage();
            message.HighPriority = true;
            message.Type = MessageType.Client;
            message.Target = client2.ClientId;
            message.SetStringContent("Hello, World!");

            bool acknowledge = await client1.SendWithAcknowledge(message);
            Assert.False(acknowledge);
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
            await Task.Delay(250);

            TmqClient client = new TmqClient();
            client.AutoAcknowledge = true;
            client.IgnoreMyQueueMessages = false;

            await client.ConnectAsync("tmq://localhost:42304");
            Assert.True(client.IsConnected);

            Channel channel = server.Server.Channels.FirstOrDefault();
            Assert.NotNull(channel);
            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            queue.Options.RequestAcknowledge = true;

            //subscribe
            await client.Join(channel.Name, true);

            //push a message to the queue
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push(channel.Name, queue.ContentType, ms, true);
            Assert.True(sent);
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel by manuel
        /// </summary>
        [Fact]
        public void FromClientToChannelManuel()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42305);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends message from channel to client and wait for acknowledge from client to channel until timed out
        /// </summary>
        [Fact]
        public void FromClientToChannelTimeout()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42306);

            throw new NotImplementedException();
        }

        #endregion
    }
}