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
            server.Initialize(42601);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";

            await client1.ConnectAsync("tmq://localhost:42601");
            await client2.ConnectAsync("tmq://localhost:42601");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool received = false;
            client2.MessageReceived += (c, m) => received = m.Source == "client-1";

            TmqMessage message = new TmqMessage(MessageType.Client, "client-2");
            message.SetStringContent("Hello, World!");

            bool sent = await client1.SendAsync(message);
            Assert.True(sent);
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
            server.Initialize(42602);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = true;

            await client1.ConnectAsync("tmq://localhost:42602");
            await client2.ConnectAsync("tmq://localhost:42602");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            bool received = false;
            client2.MessageReceived += (c, m) => received = m.Source == "client-1";

            TmqMessage message = new TmqMessage(MessageType.Client, "client-2");
            message.SetStringContent("Hello, World!");

            bool sent = await client1.SendWithAcknowledge(message);
            Assert.True(sent);
            Assert.True(received);
        }

        /// <summary>
        /// Sends a client message and waits response
        /// </summary>
        [Fact]
        public async Task WithResponse()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(42603);
            server.Start();

            TmqClient client1 = new TmqClient();
            TmqClient client2 = new TmqClient();

            client1.ClientId = "client-1";
            client2.ClientId = "client-2";
            client2.AutoAcknowledge = true;

            await client1.ConnectAsync("tmq://localhost:42603");
            await client2.ConnectAsync("tmq://localhost:42603");

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);

            client2.MessageReceived += async (c, m) =>
            {
                if (m.Source == "client-1")
                {
                    TmqMessage rmsg = m.CreateResponse();
                    rmsg.ContentType = 123;
                    rmsg.SetStringContent("Hello, World Response!");
                    await ((TmqClient) c).SendAsync(rmsg);
                }
            };

            TmqMessage message = new TmqMessage(MessageType.Client, "client-2");
            message.SetStringContent("Hello, World!");

            TmqMessage response = await client1.Request(message);
            Assert.NotNull(response);
            Assert.Equal(123, response.ContentType);
        }
    }
}