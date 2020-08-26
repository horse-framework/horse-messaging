using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    public class ClientMessageTest
    {
        /// <summary>
        /// Sends a client message and does not wait any ack or response
        /// </summary>
        [Fact]
        public async Task WithoutAnyResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";

            await client1.ConnectAsync("tmq://localhost:" + port);
            await client2.ConnectAsync("tmq://localhost:" + port);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool received = false;
            client2.MessageReceived += (c, m) => received = m.Source == "client-1";

            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, "client-2");
            message.SetStringContent("Hello, World!");

            TwinoResult sent = await client1.SendAsync(message);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
            await Task.Delay(1000);
            Assert.True(received);
        }

        /// <summary>
        /// Sends a client message and waits acknowledge
        /// </summary>
        [Fact]
        public async Task WithAcknowledge()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = true;
            client1.AcknowledgeTimeout = TimeSpan.FromMinutes(14);

            await client1.ConnectAsync("tmq://localhost:" + port);
            await client2.ConnectAsync("tmq://localhost:" + port);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool received = false;
            client2.MessageReceived += (c, m) => received = m.Source == "client-1";

            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, "client-2");
            message.SetStringContent("Hello, World!");

            TwinoResult sent = await client1.SendWithAcknowledge(message);
            Assert.Equal(TwinoResultCode.Ok, sent.Code);
            Assert.True(received);
        }

        /// <summary>
        /// Sends a client message and waits response
        /// </summary>
        [Fact]
        public async Task WithResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize();
            int port = server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = true;

            await client1.ConnectAsync("tmq://localhost:" + port);
            await client2.ConnectAsync("tmq://localhost:" + port);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            client2.MessageReceived += async (c, m) =>
            {
                if (m.Source == "client-1")
                {
                    TwinoMessage rmsg = m.CreateResponse(TwinoResultCode.Ok);
                    rmsg.SetStringContent("Hello, World Response!");
                    await ((TmqClient) c).SendAsync(rmsg);
                }
            };

            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, "client-2");
            message.SetStringContent("Hello, World!");

            TwinoMessage response = await client1.Request(message);
            Assert.NotNull(response);
            Assert.Equal(0, response.ContentType);
        }
    }
}